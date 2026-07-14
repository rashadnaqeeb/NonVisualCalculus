use std::path::{Path, PathBuf};

// The full release list, newest first: one call serves both the latest-version
// lookup and the per-version release notes shown after an update.
pub const GITHUB_RELEASES_URL: &str =
    "https://api.github.com/repos/rashadnaqeeb/NonVisualCalculus/releases?per_page=100";
pub const MOD_ZIP_PREFIX: &str = "NonVisualCalculus-v";
pub const MOD_ZIP_SUFFIX: &str = ".zip";
// Steam and GOG name the executable disco.exe; the Epic Games Store build of
// the same game names it "Disco Elysium.exe" (and its data folder
// "Disco Elysium_Data" instead of "disco_Data").
pub const GAME_EXES: &[&str] = &["disco.exe", "Disco Elysium.exe"];
// The game is IL2CPP: GameAssembly.dll holds the compiled game code and sits
// next to the exe in every install, so together they identify the game dir.
pub const IL2CPP_MARKER: &str = "GameAssembly.dll";
pub const PLUGIN_REL: &str = "BepInEx/plugins/NonVisualCalculus/NonVisualCalculus.dll";
pub const MANIFEST_REL: &str = "BepInEx/config/NonVisualCalculus/install.json";
pub const BACKUPS_REL: &str = "BepInEx/config/NonVisualCalculus/backups";
// The mod's pre-rename release, Whirling in Words. An install adopts its manifest and removes its
// plugin folder, or BepInEx would load both mods and speak everything twice. Its backups folder is
// NOT cleaned up: the adopted manifest's backup entries point into it until uninstall restores them.
pub const LEGACY_PLUGIN_DIR_REL: &str = "BepInEx/plugins/WhirlingInWords";
pub const LEGACY_MANIFEST_REL: &str = "BepInEx/config/WhirlingInWords/install.json";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
}

pub fn legacy_manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(LEGACY_MANIFEST_REL)
}

pub fn normalize_rel(path: &str) -> String {
    path.replace('\\', "/").trim_start_matches("./").to_string()
}

pub fn required_loader_files() -> &'static [&'static str] {
    &[
        "winhttp.dll",
        "doorstop_config.ini",
        "BepInEx/core/BepInEx.Core.dll",
        PLUGIN_REL,
    ]
}
