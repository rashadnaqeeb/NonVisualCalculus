use std::path::{Path, PathBuf};

pub const GITHUB_API_URL: &str =
    "https://api.github.com/repos/rashadnaqeeb/WhirlingInWords/releases/latest";
pub const MOD_ZIP_PREFIX: &str = "WhirlingInWords-v";
pub const MOD_ZIP_SUFFIX: &str = ".zip";
pub const GAME_EXE: &str = "disco.exe";
// The game is IL2CPP: GameAssembly.dll holds the compiled game code and sits
// next to disco.exe in every install, so together they identify the game dir.
pub const IL2CPP_MARKER: &str = "GameAssembly.dll";
pub const PLUGIN_REL: &str = "BepInEx/plugins/WhirlingInWords/WhirlingInWords.dll";
pub const MANIFEST_REL: &str = "BepInEx/config/WhirlingInWords/install.json";
pub const BACKUPS_REL: &str = "BepInEx/config/WhirlingInWords/backups";

pub fn manifest_path(game_dir: &Path) -> PathBuf {
    game_dir.join(MANIFEST_REL)
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
