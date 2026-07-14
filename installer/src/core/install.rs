use std::collections::{HashMap, HashSet};
use std::fs;
use std::io::{Read, Seek};
use std::path::{Component, Path, PathBuf};

use semver::Version;
use sha2::{Digest, Sha256};
use time::OffsetDateTime;
use time::format_description::well_known::Rfc3339;
use zip::ZipArchive;

use super::detect::GameSource;
use super::github::Asset;
use super::manifest::{InstallManifest, ManifestRead, SUPPORTED_SCHEMA};
use super::paths;
use super::uninstall;

#[derive(Debug, Clone)]
pub enum InstallState {
    Fresh,
    Managed(InstallManifest),
    Unmanaged,
    DamagedState(String),
}

pub fn classify_install(game_dir: &Path) -> InstallState {
    match InstallManifest::read(game_dir) {
        ManifestRead::Valid(manifest) => InstallState::Managed(manifest),
        ManifestRead::Missing => {
            if has_installed_mod_files(game_dir) {
                InstallState::Unmanaged
            } else {
                InstallState::Fresh
            }
        }
        ManifestRead::Invalid(reason) => {
            if has_installed_mod_files(game_dir) {
                InstallState::DamagedState(reason)
            } else {
                InstallState::Fresh
            }
        }
    }
}

pub fn has_installed_mod_files(game_dir: &Path) -> bool {
    paths::required_loader_files()
        .iter()
        .any(|rel| game_dir.join(rel).exists())
}

pub fn installed_version(state: &InstallState) -> Option<Version> {
    match state {
        InstallState::Managed(manifest) => Version::parse(&manifest.mod_version).ok(),
        _ => None,
    }
}

pub fn verify_sha256(path: &Path, expected: &str) -> Result<(), String> {
    let actual = sha256_file(path)?;
    if !actual.eq_ignore_ascii_case(expected) {
        return Err(format!(
            "Downloaded zip digest mismatch. Expected {expected}, got {actual}."
        ));
    }
    Ok(())
}

/// Clear the read-only attribute so the file can be overwritten or removed.
/// Windows refuses both on a read-only file even for an elevated process
/// (e.g. a `.doorstop_version` extracted read-only by a manual BepInEx unzip).
pub fn ensure_writable(path: &Path) -> Result<(), String> {
    let Ok(metadata) = fs::metadata(path) else {
        return Ok(());
    };
    let mut permissions = metadata.permissions();
    if permissions.readonly() {
        permissions.set_readonly(false);
        fs::set_permissions(path, permissions).map_err(|e| {
            format!(
                "Failed to clear the read-only attribute on {}: {e}",
                path.display()
            )
        })?;
    }
    Ok(())
}

pub fn sha256_file(path: &Path) -> Result<String, String> {
    let mut file =
        fs::File::open(path).map_err(|e| format!("Failed to open file for hashing: {e}"))?;
    let mut hasher = Sha256::new();
    let mut buffer = [0u8; 81920];
    loop {
        let read = file
            .read(&mut buffer)
            .map_err(|e| format!("Failed to read file for hashing: {e}"))?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }
    let digest = hasher.finalize();
    Ok(digest.iter().map(|b| format!("{b:02x}")).collect())
}

pub fn install_from_zip(
    zip_path: &Path,
    game_dir: &Path,
    source: &GameSource,
    asset: &Asset,
    prior_state: &InstallState,
) -> Result<InstallManifest, String> {
    let version = asset
        .version()
        .ok_or_else(|| format!("Release asset name is not a mod zip: {}", asset.name))?;
    let prior_manifest = match prior_state {
        InstallState::Managed(manifest) => Some(manifest),
        _ => None,
    };
    let mut backups = prior_manifest
        .map(|m| m.backups.clone())
        .unwrap_or_else(HashMap::new);
    let prior_owned: HashSet<String> = prior_manifest
        .map(|m| m.installed_files.iter().cloned().collect())
        .unwrap_or_default();
    let backup_stamp = backup_stamp()?;
    let mut installed_files = Vec::new();

    let file = fs::File::open(zip_path).map_err(|e| format!("Failed to open zip: {e}"))?;
    let mut archive = ZipArchive::new(file).map_err(|e| format!("Failed to read zip: {e}"))?;
    extract_archive(
        &mut archive,
        game_dir,
        &prior_owned,
        &mut backups,
        &backup_stamp,
        &mut installed_files,
    )?;

    prune_orphans(game_dir, &prior_owned, &installed_files, &mut backups)?;
    remove_legacy_plugin_dir(game_dir)?;

    let sha256 = asset.sha256_digest().or_else(|| sha256_file(zip_path).ok());
    let manifest = InstallManifest {
        schema_version: SUPPORTED_SCHEMA,
        mod_version: version,
        installed_at: OffsetDateTime::now_utc()
            .format(&Rfc3339)
            .unwrap_or_else(|_| "unknown".to_string()),
        source: source.as_manifest_str().to_string(),
        release_asset: asset.name.clone(),
        sha256,
        installed_files,
        backups,
    };
    manifest.write(game_dir)?;
    remove_legacy_manifest(game_dir)?;
    Ok(manifest)
}

/// Remove files the prior install owned that the new zip no longer ships, so an upgrade never
/// strands a stale file - above all the pre-rename plugin DLLs, which would load as a second mod.
/// A file the prior install had backed up (it overwrote something pre-existing) is restored from
/// that backup instead, the same as uninstall would.
fn prune_orphans(
    game_dir: &Path,
    prior_owned: &HashSet<String>,
    installed_files: &[String],
    backups: &mut HashMap<String, String>,
) -> Result<(), String> {
    let current: HashSet<&String> = installed_files.iter().collect();
    for rel in prior_owned {
        if current.contains(rel) {
            continue;
        }
        let target = game_dir.join(rel);
        ensure_writable(&target)?;
        if let Some(backup_rel) = backups.remove(rel) {
            let backup = game_dir.join(&backup_rel);
            if backup.exists() {
                fs::copy(&backup, &target)
                    .map_err(|e| format!("Failed to restore {}: {e}", target.display()))?;
                fs::remove_file(&backup)
                    .map_err(|e| format!("Failed to remove backup {}: {e}", backup.display()))?;
                uninstall::remove_empty_parents(game_dir, backup.parent());
                continue;
            }
        }
        if target.exists() {
            fs::remove_file(&target)
                .map_err(|e| format!("Failed to remove {}: {e}", target.display()))?;
            uninstall::remove_empty_parents(game_dir, target.parent());
        }
    }
    Ok(())
}

/// The pre-rename Whirling in Words plugin folder. Its manifest-listed files are already pruned;
/// this catches a manual unzip or files no manifest tracked. The old backups folder (under
/// BepInEx/config) is deliberately left alone - the adopted manifest's backups point into it.
fn remove_legacy_plugin_dir(game_dir: &Path) -> Result<(), String> {
    let dir = game_dir.join(paths::LEGACY_PLUGIN_DIR_REL);
    if !dir.exists() {
        return Ok(());
    }
    make_tree_writable(&dir)?;
    fs::remove_dir_all(&dir).map_err(|e| {
        format!(
            "Failed to remove the old Whirling in Words plugin folder {}: {e}",
            dir.display()
        )
    })
}

fn remove_legacy_manifest(game_dir: &Path) -> Result<(), String> {
    let old = paths::legacy_manifest_path(game_dir);
    if !old.exists() {
        return Ok(());
    }
    ensure_writable(&old)?;
    fs::remove_file(&old)
        .map_err(|e| format!("Failed to remove the old manifest {}: {e}", old.display()))?;
    uninstall::remove_empty_parents(game_dir, old.parent());
    Ok(())
}

fn make_tree_writable(dir: &Path) -> Result<(), String> {
    let entries =
        fs::read_dir(dir).map_err(|e| format!("Failed to list {}: {e}", dir.display()))?;
    for entry in entries {
        let path = entry
            .map_err(|e| format!("Failed to list {}: {e}", dir.display()))?
            .path();
        if path.is_dir() {
            make_tree_writable(&path)?;
        } else {
            ensure_writable(&path)?;
        }
    }
    Ok(())
}

fn extract_archive<R: Read + Seek>(
    archive: &mut ZipArchive<R>,
    game_dir: &Path,
    prior_owned: &HashSet<String>,
    backups: &mut HashMap<String, String>,
    backup_stamp: &str,
    installed_files: &mut Vec<String>,
) -> Result<(), String> {
    for i in 0..archive.len() {
        let mut entry = archive
            .by_index(i)
            .map_err(|e| format!("Failed to read zip entry: {e}"))?;
        let raw_name = entry.name().to_string();
        let Some(rel) = safe_zip_entry_name(&raw_name) else {
            return Err(format!("Unsafe zip entry path: {raw_name}"));
        };
        if rel.as_os_str().is_empty() {
            continue;
        }
        let dest = game_dir.join(&rel);
        if entry.is_dir() {
            fs::create_dir_all(&dest)
                .map_err(|e| format!("Failed to create directory {}: {e}", dest.display()))?;
            continue;
        }

        let rel_key = paths::normalize_rel(rel.to_string_lossy().as_ref());
        if dest.exists() && !prior_owned.contains(&rel_key) && !backups.contains_key(&rel_key) {
            backup_file(game_dir, &rel_key, backups, backup_stamp)?;
        }

        if let Some(parent) = dest.parent() {
            fs::create_dir_all(parent)
                .map_err(|e| format!("Failed to create parent directory: {e}"))?;
        }
        ensure_writable(&dest)?;
        let mut output = fs::File::create(&dest)
            .map_err(|e| format!("Failed to create {}: {e}", dest.display()))?;
        std::io::copy(&mut entry, &mut output)
            .map_err(|e| format!("Failed to write {}: {e}", dest.display()))?;
        installed_files.push(rel_key);
    }
    Ok(())
}

fn backup_file(
    game_dir: &Path,
    rel_key: &str,
    backups: &mut HashMap<String, String>,
    backup_stamp: &str,
) -> Result<(), String> {
    let src = game_dir.join(rel_key);
    if !src.exists() {
        return Ok(());
    }
    let backup_rel = paths::normalize_rel(
        Path::new(paths::BACKUPS_REL)
            .join(backup_stamp)
            .join(rel_key)
            .to_string_lossy()
            .as_ref(),
    );
    let backup_abs = game_dir.join(&backup_rel);
    if let Some(parent) = backup_abs.parent() {
        fs::create_dir_all(parent)
            .map_err(|e| format!("Failed to create backup directory: {e}"))?;
    }
    fs::copy(&src, &backup_abs).map_err(|e| format!("Failed to back up {}: {e}", src.display()))?;
    // fs::copy carries the read-only attribute onto the backup, which would
    // block removing it on uninstall.
    ensure_writable(&backup_abs)?;
    backups.insert(rel_key.to_string(), backup_rel);
    Ok(())
}

fn backup_stamp() -> Result<String, String> {
    let now = OffsetDateTime::now_utc()
        .format(&Rfc3339)
        .map_err(|e| format!("Failed to format backup timestamp: {e}"))?;
    Ok(now
        .replace(':', "")
        .replace('-', "")
        .replace('T', "_")
        .replace('Z', "Z"))
}

pub fn temp_session_dir() -> PathBuf {
    let nanos = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_nanos())
        .unwrap_or(0);
    std::env::temp_dir()
        .join("NonVisualCalculusInstaller")
        .join(format!("{}-{nanos}", std::process::id()))
}

pub fn safe_zip_entry_name(name: &str) -> Option<PathBuf> {
    let normalized = name.replace('\\', "/");
    let path = Path::new(&normalized);
    let mut out = PathBuf::new();
    for component in path.components() {
        match component {
            Component::Normal(part) => out.push(part),
            Component::CurDir => {}
            Component::ParentDir | Component::RootDir | Component::Prefix(_) => return None,
        }
    }
    Some(out)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    use crate::core::github::Asset;
    use crate::core::uninstall;

    #[test]
    fn rejects_zip_slip_paths() {
        assert!(safe_zip_entry_name("../../evil.dll").is_none());
        assert!(safe_zip_entry_name("C:/evil.dll").is_none());
        assert!(safe_zip_entry_name("/evil.dll").is_none());
        assert!(safe_zip_entry_name("BepInEx/plugins/NonVisualCalculus/mod.dll").is_some());
    }

    #[test]
    fn classifies_unmanaged_by_file_presence_only() {
        let dir = tempfile::tempdir().unwrap();
        let plugin = dir.path().join(paths::PLUGIN_REL);
        fs::create_dir_all(plugin.parent().unwrap()).unwrap();
        fs::write(plugin, "not a real dll").unwrap();
        assert!(matches!(
            classify_install(dir.path()),
            InstallState::Unmanaged
        ));
    }

    #[test]
    fn invalid_manifest_with_files_is_damaged_state() {
        let dir = tempfile::tempdir().unwrap();
        let manifest = paths::manifest_path(dir.path());
        fs::create_dir_all(manifest.parent().unwrap()).unwrap();
        fs::write(&manifest, "{not json").unwrap();
        let plugin = dir.path().join(paths::PLUGIN_REL);
        fs::create_dir_all(plugin.parent().unwrap()).unwrap();
        fs::write(plugin, "").unwrap();
        assert!(matches!(
            classify_install(dir.path()),
            InstallState::DamagedState(_)
        ));
    }

    #[test]
    fn fresh_install_then_uninstall_leaves_game_dir_clean() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("disco.exe"), "game").unwrap();
        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[
                (paths::PLUGIN_REL, "plugin"),
                ("BepInEx/plugins/NonVisualCalculus/lang/en.txt", "strings"),
                ("winhttp.dll", "loader"),
                ("prism.dll", "speech"),
            ],
        );

        let manifest = install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Fresh,
        )
        .unwrap();

        assert!(manifest.backups.is_empty());
        assert!(dir.path().join(paths::PLUGIN_REL).exists());
        assert!(dir.path().join("prism.dll").exists());
        assert!(matches!(
            classify_install(dir.path()),
            InstallState::Managed(_)
        ));

        uninstall::uninstall(dir.path(), &manifest).unwrap();

        assert!(!dir.path().join(paths::PLUGIN_REL).exists());
        assert!(!dir.path().join("prism.dll").exists());
        assert!(!dir.path().join("BepInEx").exists());
        assert!(!paths::manifest_path(dir.path()).exists());
        assert!(dir.path().join("disco.exe").exists());
    }

    #[test]
    fn overwritten_file_is_backed_up_and_restored_on_uninstall() {
        let dir = tempfile::tempdir().unwrap();
        // A pre-existing winhttp.dll (say, a manual BepInEx install) must survive a
        // managed install + uninstall round trip.
        fs::write(dir.path().join("winhttp.dll"), "original loader").unwrap();

        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[(paths::PLUGIN_REL, "plugin"), ("winhttp.dll", "new loader")],
        );

        let manifest = install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Fresh,
        )
        .unwrap();

        let backup_rel = manifest
            .backups
            .get("winhttp.dll")
            .expect("existing file should be backed up")
            .clone();
        assert!(dir.path().join(&backup_rel).exists());
        assert_eq!(
            fs::read_to_string(dir.path().join("winhttp.dll")).unwrap(),
            "new loader"
        );

        uninstall::uninstall(dir.path(), &manifest).unwrap();

        assert_eq!(
            fs::read_to_string(dir.path().join("winhttp.dll")).unwrap(),
            "original loader"
        );
        assert!(!dir.path().join(paths::BACKUPS_REL).exists());
    }

    #[test]
    fn overwrites_and_uninstalls_read_only_files() {
        let dir = tempfile::tempdir().unwrap();
        // A read-only pre-existing file (e.g. .doorstop_version from a manual
        // BepInEx unzip) must not block install or uninstall.
        let existing = dir.path().join(".doorstop_version");
        fs::write(&existing, "4.0.0").unwrap();
        let mut perms = fs::metadata(&existing).unwrap().permissions();
        perms.set_readonly(true);
        fs::set_permissions(&existing, perms).unwrap();

        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[
                (paths::PLUGIN_REL, "plugin"),
                (".doorstop_version", "4.4.0"),
            ],
        );

        let manifest = install_from_zip(
            &zip_path,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Fresh,
        )
        .unwrap();

        assert_eq!(fs::read_to_string(&existing).unwrap(), "4.4.0");
        let backup_rel = manifest.backups.get(".doorstop_version").unwrap();
        assert_eq!(
            fs::read_to_string(dir.path().join(backup_rel)).unwrap(),
            "4.0.0"
        );

        uninstall::uninstall(dir.path(), &manifest).unwrap();

        assert_eq!(fs::read_to_string(&existing).unwrap(), "4.0.0");
        assert!(!dir.path().join(paths::BACKUPS_REL).exists());
    }

    #[test]
    fn upgrade_from_legacy_install_removes_the_old_mod() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("disco.exe"), "game").unwrap();
        // A managed pre-rename install: old-name plugin files plus a manifest at the old location.
        let old_plugin = "BepInEx/plugins/WhirlingInWords/WhirlingInWords.dll";
        let old_lang = "BepInEx/plugins/WhirlingInWords/lang/en.txt";
        for (rel, content) in [
            (old_plugin, "old plugin"),
            (old_lang, "old strings"),
            ("winhttp.dll", "loader"),
            ("prism.dll", "speech"),
        ] {
            let abs = dir.path().join(rel);
            fs::create_dir_all(abs.parent().unwrap()).unwrap();
            fs::write(abs, content).unwrap();
        }
        let legacy = InstallManifest {
            schema_version: SUPPORTED_SCHEMA,
            mod_version: "1.0.1".to_string(),
            installed_at: "2026-01-01T00:00:00Z".to_string(),
            source: "steam".to_string(),
            release_asset: "WhirlingInWords-v1.0.1.zip".to_string(),
            sha256: None,
            installed_files: vec![
                old_plugin.to_string(),
                old_lang.to_string(),
                "winhttp.dll".to_string(),
                "prism.dll".to_string(),
            ],
            backups: HashMap::new(),
        };
        let legacy_path = paths::legacy_manifest_path(dir.path());
        fs::create_dir_all(legacy_path.parent().unwrap()).unwrap();
        fs::write(&legacy_path, serde_json::to_string(&legacy).unwrap()).unwrap();

        let state = classify_install(dir.path());
        assert!(matches!(state, InstallState::Managed(_)));

        let zip_path = dir.path().join("release.zip");
        create_zip(
            &zip_path,
            &[
                (paths::PLUGIN_REL, "new plugin"),
                ("winhttp.dll", "loader"),
                ("prism.dll", "speech"),
            ],
        );
        let manifest =
            install_from_zip(&zip_path, dir.path(), &GameSource::Manual, &test_asset(), &state)
                .unwrap();

        assert!(!dir.path().join(paths::LEGACY_PLUGIN_DIR_REL).exists());
        assert!(!legacy_path.exists());
        assert!(dir.path().join(paths::PLUGIN_REL).exists());
        assert!(paths::manifest_path(dir.path()).exists());
        assert!(manifest.backups.is_empty());

        // Uninstalling the adopted install leaves the game dir clean.
        uninstall::uninstall(dir.path(), &manifest).unwrap();
        assert!(!dir.path().join("BepInEx").exists());
        assert!(!dir.path().join("prism.dll").exists());
        assert!(dir.path().join("disco.exe").exists());
    }

    #[test]
    fn orphaned_file_with_backup_is_restored_on_upgrade() {
        let dir = tempfile::tempdir().unwrap();
        // v1 shipped .doorstop_version over a pre-existing copy and backed the original up.
        fs::write(dir.path().join(".doorstop_version"), "user original").unwrap();
        let zip1 = dir.path().join("v1.zip");
        create_zip(
            &zip1,
            &[(paths::PLUGIN_REL, "plugin"), (".doorstop_version", "modded")],
        );
        let m1 = install_from_zip(
            &zip1,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Fresh,
        )
        .unwrap();
        assert!(m1.backups.contains_key(".doorstop_version"));

        // v2 no longer ships it: the original comes back and the backup is consumed.
        let zip2 = dir.path().join("v2.zip");
        create_zip(&zip2, &[(paths::PLUGIN_REL, "plugin v2")]);
        let m2 = install_from_zip(
            &zip2,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Managed(m1),
        )
        .unwrap();
        assert_eq!(
            fs::read_to_string(dir.path().join(".doorstop_version")).unwrap(),
            "user original"
        );
        assert!(m2.backups.is_empty());
        assert!(!dir.path().join(paths::BACKUPS_REL).exists());
    }

    #[test]
    fn manual_legacy_plugin_folder_is_removed_on_install() {
        let dir = tempfile::tempdir().unwrap();
        // A hand-unzipped pre-rename install: no manifest, and archive tools can leave read-only files.
        let stray = dir
            .path()
            .join(paths::LEGACY_PLUGIN_DIR_REL)
            .join("WhirlingInWords.dll");
        fs::create_dir_all(stray.parent().unwrap()).unwrap();
        fs::write(&stray, "manually unzipped").unwrap();
        let mut perms = fs::metadata(&stray).unwrap().permissions();
        perms.set_readonly(true);
        fs::set_permissions(&stray, perms).unwrap();

        let zip = dir.path().join("release.zip");
        create_zip(&zip, &[(paths::PLUGIN_REL, "plugin")]);
        install_from_zip(
            &zip,
            dir.path(),
            &GameSource::Manual,
            &test_asset(),
            &InstallState::Unmanaged,
        )
        .unwrap();
        assert!(!dir.path().join(paths::LEGACY_PLUGIN_DIR_REL).exists());
    }

    fn create_zip(path: &Path, entries: &[(&str, &str)]) {
        let file = fs::File::create(path).unwrap();
        let mut zip = zip::ZipWriter::new(file);
        let options = zip::write::SimpleFileOptions::default();
        for (name, content) in entries {
            zip.start_file(name, options).unwrap();
            zip.write_all(content.as_bytes()).unwrap();
        }
        zip.finish().unwrap();
    }

    fn test_asset() -> Asset {
        Asset {
            name: "NonVisualCalculus-v1.2.3.zip".to_string(),
            browser_download_url: "https://example.invalid/release.zip".to_string(),
            digest: None,
        }
    }
}
