use sysinfo::System;

use super::paths::GAME_EXES;

pub fn is_game_running() -> bool {
    let system = System::new_all();
    system.processes().values().any(|process| {
        let name = process.name().to_string_lossy().to_ascii_lowercase();
        GAME_EXES.iter().any(|exe| {
            let exe = exe.to_ascii_lowercase();
            name == exe || Some(name.as_str()) == exe.strip_suffix(".exe")
        })
    })
}
