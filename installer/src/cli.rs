use std::io::{self, Write};
use std::path::PathBuf;

use semver::Version;

use crate::core::detect::{self, GameInstall, GameSource};
use crate::core::github;
use crate::core::install::{self, InstallState};
use crate::core::process;
use crate::core::uninstall;
use crate::i18n::{self, Strings, fill};

pub fn run() {
    let s = i18n::get();
    println!("{}", s.cli_header);
    let Some(game) = get_game_install(s) else {
        println!("{}", s.cli_no_valid_install);
        return;
    };

    loop {
        let state = install::classify_install(&game.path);
        println!();
        println!(
            "{}",
            fill(s.log_game_dir, &[("path", &game.path.display().to_string())])
        );
        print_state(s, &state);
        println!();
        println!("{}", s.cli_menu_install);
        println!("{}", s.cli_menu_reinstall);
        println!("{}", s.cli_menu_uninstall);
        println!("{}", s.cli_menu_exit);

        match prompt(s.cli_choose).as_str() {
            "1" => install_latest(s, &game, false),
            "2" => install_latest(s, &game, true),
            "3" => uninstall_managed(s, &game.path, &state),
            "4" => return,
            _ => println!("{}", s.cli_invalid_option),
        }
    }
}

fn get_game_install(s: &Strings) -> Option<GameInstall> {
    if let Some(detected) = detect::detect_game() {
        println!(
            "{}",
            fill(
                s.log_detected_dir,
                &[("path", &detected.path.display().to_string())]
            )
        );
        let answer = prompt(s.cli_use_path);
        if !is_no(s, &answer) {
            return Some(detected);
        }
    }

    let input = prompt(s.cli_type_path);
    let path = PathBuf::from(input.trim_matches('"'));
    if detect::validate_game_dir(&path) {
        Some(GameInstall {
            path,
            source: GameSource::Manual,
        })
    } else {
        None
    }
}

fn print_state(s: &Strings, state: &InstallState) {
    match state {
        InstallState::Fresh => println!("{}", s.cli_state_not_installed),
        InstallState::Managed(manifest) => {
            println!(
                "{}",
                fill(s.cli_state_installed, &[("version", &manifest.mod_version)])
            );
        }
        InstallState::Unmanaged => {
            println!("{}", s.cli_state_unmanaged);
        }
        InstallState::DamagedState(reason) => {
            println!("{}", fill(s.cli_state_damaged, &[("reason", reason)]));
        }
    }
}

fn install_latest(s: &Strings, game: &GameInstall, force: bool) {
    if process::is_game_running() {
        println!("{}", s.err_close_game_install);
        return;
    }

    let state = install::classify_install(&game.path);
    let release = match github::fetch_latest_release() {
        Ok(r) => r,
        Err(e) => {
            println!("{}", fill(s.cli_error, &[("error", &e)]));
            return;
        }
    };
    let Some(asset) = github::find_mod_zip(&release) else {
        println!("{}", s.log_no_asset);
        return;
    };

    if !force {
        if let (Some(installed), Some(latest)) = (
            install::installed_version(&state),
            asset.version().and_then(|v| Version::parse(&v).ok()),
        ) {
            if installed >= latest {
                println!("{}", s.msg_already_up_to_date);
                return;
            }
        }
    }

    let temp_dir = install::temp_session_dir();
    let zip_path = temp_dir.join(&asset.name);
    println!("{}", fill(s.log_downloading, &[("name", &asset.name)]));
    let result = (|| {
        github::download_asset(&asset, &zip_path)?;
        if let Some(expected) = asset.sha256_digest() {
            println!("{}", s.log_verifying);
            install::verify_sha256(&zip_path, &expected)?;
        }
        println!("{}", s.log_installing);
        install::install_from_zip(&zip_path, &game.path, &game.source, &asset, &state)?;
        Ok::<(), String>(())
    })();
    let _ = std::fs::remove_dir_all(&temp_dir);

    match result {
        Ok(()) => {
            println!("{}", s.msg_install_complete);
            println!("{}", s.msg_first_launch_note);
        }
        Err(e) => println!("{}", fill(s.cli_error, &[("error", &e)])),
    }
}

fn uninstall_managed(s: &Strings, game_path: &std::path::Path, state: &InstallState) {
    if process::is_game_running() {
        println!("{}", s.err_close_game_uninstall);
        return;
    }
    let InstallState::Managed(manifest) = state else {
        println!("{}", s.err_uninstall_managed_only);
        return;
    };
    let answer = prompt(s.cli_confirm_uninstall);
    if !is_yes(s, &answer) {
        return;
    }
    match uninstall::uninstall(game_path, manifest) {
        Ok(()) => println!("{}", s.msg_uninstall_complete),
        Err(e) => println!("{}", fill(s.cli_error, &[("error", &e)])),
    }
}

// Yes/no answers accept the localized key from the prompt plus English y/n.
fn is_yes(s: &Strings, answer: &str) -> bool {
    answer == s.cli_yes_key || answer == "y" || answer == "yes"
}

fn is_no(s: &Strings, answer: &str) -> bool {
    answer == s.cli_no_key || answer == "n" || answer == "no"
}

fn prompt(message: &str) -> String {
    print!("{message}");
    let _ = io::stdout().flush();
    let mut input = String::new();
    let _ = io::stdin().read_line(&mut input);
    input.trim().to_lowercase()
}
