use std::collections::HashSet;
use std::fs;
use std::path::{Path, PathBuf};

use regex::Regex;

use super::paths::{GAME_EXES, IL2CPP_MARKER};

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum GameSource {
    Steam,
    Gog,
    Epic,
    Manual,
}

impl GameSource {
    pub fn as_manifest_str(&self) -> &'static str {
        match self {
            GameSource::Steam => "steam",
            GameSource::Gog => "gog",
            GameSource::Epic => "epic",
            GameSource::Manual => "manual",
        }
    }
}

#[derive(Debug, Clone)]
pub struct GameInstall {
    pub path: PathBuf,
    pub source: GameSource,
}

pub fn detect_game() -> Option<GameInstall> {
    let candidates = game_candidates();
    for candidate in candidates {
        if validate_game_dir(&candidate.path) {
            return Some(candidate);
        }
    }
    None
}

pub fn validate_game_dir(path: &Path) -> bool {
    missing_marker(path).is_none()
}

/// The required file `path` lacks, named for diagnostics; None when the
/// directory is a valid game install.
pub fn missing_marker(path: &Path) -> Option<String> {
    if !GAME_EXES.iter().any(|exe| path.join(exe).exists()) {
        return Some(GAME_EXES.join(" / "));
    }
    if !path.join(IL2CPP_MARKER).exists() {
        return Some(IL2CPP_MARKER.to_string());
    }
    None
}

pub fn game_candidates() -> Vec<GameInstall> {
    let mut result = Vec::new();
    let mut seen = HashSet::new();

    if let Ok(value) = std::env::var("DISCO_ELYSIUM_DIR") {
        add_candidate(
            &mut result,
            &mut seen,
            PathBuf::from(value),
            GameSource::Manual,
        );
    }

    for path in steam_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Steam);
    }

    for path in gog_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Gog);
    }

    for path in epic_candidates() {
        add_candidate(&mut result, &mut seen, path, GameSource::Epic);
    }

    result
}

fn add_candidate(
    result: &mut Vec<GameInstall>,
    seen: &mut HashSet<String>,
    path: PathBuf,
    source: GameSource,
) {
    let normalized = path.to_string_lossy().trim().trim_matches('"').to_string();
    if normalized.is_empty() {
        return;
    }
    let key = normalized.to_lowercase();
    if seen.insert(key) {
        result.push(GameInstall {
            path: PathBuf::from(normalized),
            source,
        });
    }
}

pub fn parse_steam_library_paths(content: &str) -> Vec<PathBuf> {
    let re = Regex::new(r#""path"\s+"([^"]+)""#).unwrap();
    re.captures_iter(content)
        .map(|cap| PathBuf::from(cap[1].replace("\\\\", "\\")))
        .collect()
}

fn steam_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();

    for steam_root in steam_roots() {
        candidates.push(
            steam_root
                .join("steamapps")
                .join("common")
                .join("Disco Elysium"),
        );
        let vdf = steam_root.join("steamapps").join("libraryfolders.vdf");
        if let Ok(content) = fs::read_to_string(vdf) {
            for lib in parse_steam_library_paths(&content) {
                candidates.push(lib.join("steamapps").join("common").join("Disco Elysium"));
            }
        }
    }

    candidates
}

fn steam_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();

    #[cfg(windows)]
    {
        use winreg::RegKey;
        use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};

        if let Ok(key) = RegKey::predef(HKEY_CURRENT_USER).open_subkey("Software\\Valve\\Steam") {
            if let Ok(path) = key.get_value::<String, _>("SteamPath") {
                roots.push(PathBuf::from(path.replace('/', "\\")));
            }
        }
        if let Ok(key) =
            RegKey::predef(HKEY_LOCAL_MACHINE).open_subkey("SOFTWARE\\WOW6432Node\\Valve\\Steam")
        {
            if let Ok(path) = key.get_value::<String, _>("InstallPath") {
                roots.push(PathBuf::from(path));
            }
        }
    }

    roots.push(PathBuf::from("C:\\Program Files (x86)\\Steam"));
    roots
}

fn gog_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();

    #[cfg(windows)]
    {
        candidates.extend(gog_registry_candidates());
    }

    candidates.push(PathBuf::from("C:\\GOG Games\\Disco Elysium"));
    candidates.push(PathBuf::from(
        "C:\\Program Files (x86)\\GOG Galaxy\\Games\\Disco Elysium",
    ));
    candidates.push(PathBuf::from(
        "C:\\Program Files\\GOG Galaxy\\Games\\Disco Elysium",
    ));

    candidates
}

#[cfg(windows)]
fn gog_registry_candidates() -> Vec<PathBuf> {
    use winreg::RegKey;
    use winreg::enums::{HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE};

    let mut candidates = Vec::new();
    for hive in [HKEY_LOCAL_MACHINE, HKEY_CURRENT_USER] {
        let root = RegKey::predef(hive);
        for subkey in [
            "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
            "SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall",
        ] {
            if let Ok(key) = root.open_subkey(subkey) {
                for name in key.enum_keys().flatten() {
                    if let Ok(app) = key.open_subkey(name) {
                        let display = app
                            .get_value::<String, _>("DisplayName")
                            .unwrap_or_default();
                        if !display.to_lowercase().contains("disco elysium") {
                            continue;
                        }
                        if let Ok(path) = app.get_value::<String, _>("InstallLocation") {
                            candidates.push(PathBuf::from(path));
                        }
                        if let Ok(path) = app.get_value::<String, _>("InstallDir") {
                            candidates.push(PathBuf::from(path));
                        }
                    }
                }
            }
        }
    }
    candidates
}

fn epic_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();

    // The Epic launcher keeps one JSON manifest per installed game under
    // ProgramData; InstallLocation is the game folder.
    let manifests_dir = std::env::var("ProgramData")
        .map(PathBuf::from)
        .unwrap_or_else(|_| PathBuf::from("C:\\ProgramData"))
        .join("Epic")
        .join("EpicGamesLauncher")
        .join("Data")
        .join("Manifests");
    if let Ok(entries) = fs::read_dir(&manifests_dir) {
        for entry in entries.flatten() {
            let path = entry.path();
            if path.extension().is_none_or(|ext| ext != "item") {
                continue;
            }
            if let Ok(content) = fs::read_to_string(&path) {
                candidates.extend(parse_epic_manifest(&content));
            }
        }
    }

    candidates.push(PathBuf::from("C:\\Program Files\\Epic Games\\DiscoElysium"));

    candidates
}

pub fn parse_epic_manifest(content: &str) -> Option<PathBuf> {
    let manifest: serde_json::Value = serde_json::from_str(content).ok()?;
    let display = manifest.get("DisplayName")?.as_str()?;
    if !display.to_lowercase().contains("disco elysium") {
        return None;
    }
    let location = manifest.get("InstallLocation")?.as_str()?;
    Some(PathBuf::from(location))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_steam_library_paths() {
        let content = r#"
        "0" { "path" "C:\\Program Files (x86)\\Steam" }
        "1" { "path" "D:\\SteamLibrary" }
        "#;
        let paths = parse_steam_library_paths(content);
        assert_eq!(paths.len(), 2);
        assert_eq!(paths[1], PathBuf::from("D:\\SteamLibrary"));
    }

    #[test]
    fn validates_game_dir_markers() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("disco.exe"), "").unwrap();
        fs::write(dir.path().join(IL2CPP_MARKER), "").unwrap();
        assert!(validate_game_dir(dir.path()));
    }

    #[test]
    fn validates_epic_exe_name() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("Disco Elysium.exe"), "").unwrap();
        fs::write(dir.path().join(IL2CPP_MARKER), "").unwrap();
        assert!(validate_game_dir(dir.path()));
    }

    #[test]
    fn rejects_dir_without_game_assembly() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join("disco.exe"), "").unwrap();
        assert!(!validate_game_dir(dir.path()));
        assert_eq!(
            missing_marker(dir.path()),
            Some(IL2CPP_MARKER.to_string())
        );
    }

    #[test]
    fn missing_marker_names_the_exe_first() {
        let dir = tempfile::tempdir().unwrap();
        fs::write(dir.path().join(IL2CPP_MARKER), "").unwrap();
        assert_eq!(
            missing_marker(dir.path()),
            Some("disco.exe / Disco Elysium.exe".to_string())
        );
    }

    #[test]
    fn parses_epic_manifest_for_disco_elysium_only() {
        let manifest = r#"{
            "FormatVersion": 0,
            "DisplayName": "Disco Elysium - The Final Cut",
            "InstallLocation": "C:\\Program Files\\Epic Games\\DiscoElysium"
        }"#;
        assert_eq!(
            parse_epic_manifest(manifest),
            Some(PathBuf::from("C:\\Program Files\\Epic Games\\DiscoElysium"))
        );

        let other = r#"{ "DisplayName": "Fortnite", "InstallLocation": "C:\\Fortnite" }"#;
        assert_eq!(parse_epic_manifest(other), None);
        assert_eq!(parse_epic_manifest("{not json"), None);
    }
}
