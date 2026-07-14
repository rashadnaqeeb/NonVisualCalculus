use semver::Version;
use wxdragon::prelude::*;

use crate::core::detect::{self, GameInstall, GameSource};
use crate::core::github::{self, Asset, ReleaseInfo};
use crate::core::install::{self, InstallState};
use crate::core::process;
use crate::core::uninstall;
use crate::i18n::{self, Strings, fill};

pub fn run() {
    let s = i18n::get();
    wxdragon::main(move |_app| {
        let frame = Frame::builder()
            .with_title(s.app_title)
            .with_size(Size::new(680, 430))
            .build();

        let panel = Panel::builder(&frame).build();
        let main_sizer = BoxSizer::builder(Orientation::Vertical).build();

        let status = StaticText::builder(&panel)
            .with_label(s.status_detecting)
            .build();

        let path_sizer = BoxSizer::builder(Orientation::Horizontal).build();
        let path_label = StaticText::builder(&panel)
            .with_label(s.game_dir_label)
            .build();
        let path_input = TextCtrl::builder(&panel).build();
        let browse_btn = Button::builder(&panel).with_label(s.browse).build();

        path_sizer.add(&path_label, 0, SizerFlag::All, 4);
        path_sizer.add(&path_input, 1, SizerFlag::Expand | SizerFlag::All, 4);
        path_sizer.add(&browse_btn, 0, SizerFlag::All, 4);

        let log = TextCtrl::builder(&panel)
            .with_style(
                TextCtrlStyle::MultiLine | TextCtrlStyle::ReadOnly | TextCtrlStyle::WordWrap,
            )
            .build();

        let btn_sizer = BoxSizer::builder(Orientation::Horizontal).build();
        let install_btn = Button::builder(&panel).with_label(s.btn_install).build();
        let reinstall_btn = Button::builder(&panel).with_label(s.btn_reinstall).build();
        let uninstall_btn = Button::builder(&panel).with_label(s.btn_uninstall).build();
        let close_btn = Button::builder(&panel).with_label(s.btn_close).build();

        btn_sizer.add_stretch_spacer(1);
        btn_sizer.add(&install_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&reinstall_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&uninstall_btn, 0, SizerFlag::All, 4);
        btn_sizer.add(&close_btn, 0, SizerFlag::All, 4);

        main_sizer.add(&status, 0, SizerFlag::Expand | SizerFlag::All, 8);
        main_sizer.add_sizer(
            &path_sizer,
            0,
            SizerFlag::Expand | SizerFlag::Left | SizerFlag::Right,
            8,
        );
        main_sizer.add(&log, 1, SizerFlag::Expand | SizerFlag::All, 8);
        main_sizer.add_sizer(&btn_sizer, 0, SizerFlag::Expand | SizerFlag::All, 4);
        panel.set_sizer(main_sizer, true);

        install_btn.enable(false);
        reinstall_btn.enable(false);
        uninstall_btn.enable(false);

        let releases = match github::fetch_releases() {
            Ok(releases) => {
                log_append(&log, s.log_connected);
                releases
            }
            Err(e) => {
                log_append(&log, &fill(s.log_github_error, &[("error", &e)]));
                Vec::new()
            }
        };
        let asset = github::latest_release(&releases).and_then(github::find_mod_zip);
        if let Some(asset) = asset.as_ref() {
            log_append(&log, &fill(s.log_latest_asset, &[("name", &asset.name)]));
        } else {
            log_append(&log, s.log_no_asset);
        }

        let detected = detect::detect_game();
        if let Some(detected) = detected.as_ref() {
            path_input.set_value(&detected.path.to_string_lossy());
            log_append(
                &log,
                &fill(
                    s.log_detected_dir,
                    &[("path", &detected.path.display().to_string())],
                ),
            );
        } else {
            status.set_label(s.status_not_found);
            log_append(&log, s.log_could_not_detect);
            // Name every folder that exists but failed validation, so a bug
            // report says which file the rejected install was missing.
            for candidate in detect::game_candidates() {
                if !candidate.path.is_dir() {
                    continue;
                }
                if let Some(missing) = detect::missing_marker(&candidate.path) {
                    log_append(
                        &log,
                        &fill(
                            s.log_invalid_dir,
                            &[
                                ("path", &candidate.path.display().to_string()),
                                ("missing", &missing),
                            ],
                        ),
                    );
                }
            }
        }

        refresh_state(
            s,
            &path_input,
            detected.as_ref(),
            &status,
            &install_btn,
            &reinstall_btn,
            &uninstall_btn,
            asset.as_ref(),
            &log,
        );

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();
            let detected_c = detected.clone();

            browse_btn.on_click(move |_| {
                let dialog = DirDialog::builder(&frame_c, s.dir_dialog_title, "").build();
                if dialog.show_modal() != ID_OK {
                    return;
                }
                let Some(path_str) = dialog.get_path() else {
                    return;
                };
                let path = std::path::PathBuf::from(&path_str);
                if let Some(missing) = detect::missing_marker(&path) {
                    log_append(
                        &log_c,
                        &fill(
                            s.log_invalid_dir,
                            &[
                                ("path", &path.display().to_string()),
                                ("missing", &missing),
                            ],
                        ),
                    );
                    status_c.set_label(s.status_invalid_dir);
                    return;
                }
                path_input_c.set_value(&path.to_string_lossy());
                log_append(
                    &log_c,
                    &fill(s.log_game_dir, &[("path", &path.display().to_string())]),
                );
                refresh_state(
                    s,
                    &path_input_c,
                    detected_c.as_ref(),
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    asset_c.as_ref(),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();
            let detected_c = detected.clone();
            let releases_c = releases.clone();

            install_btn.on_click(move |_| {
                let Some(asset) = asset_c.as_ref() else {
                    show_logged_error(s, &frame_c, &log_c, s.err_no_zip);
                    return;
                };
                let Some(game) = game_from_input(&path_input_c, detected_c.as_ref()) else {
                    show_logged_error(s, &frame_c, &log_c, s.err_select_valid);
                    return;
                };
                install_asset(s, &frame_c, &game, asset, false, &releases_c, &log_c);
                refresh_state(
                    s,
                    &path_input_c,
                    detected_c.as_ref(),
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    Some(asset),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();
            let detected_c = detected.clone();
            let releases_c = releases.clone();

            reinstall_btn.on_click(move |_| {
                let Some(asset) = asset_c.as_ref() else {
                    show_logged_error(s, &frame_c, &log_c, s.err_no_zip);
                    return;
                };
                let Some(game) = game_from_input(&path_input_c, detected_c.as_ref()) else {
                    show_logged_error(s, &frame_c, &log_c, s.err_select_valid);
                    return;
                };
                install_asset(s, &frame_c, &game, asset, true, &releases_c, &log_c);
                refresh_state(
                    s,
                    &path_input_c,
                    detected_c.as_ref(),
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    Some(asset),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            let path_input_c = path_input.clone();
            let status_c = status.clone();
            let install_btn_c = install_btn.clone();
            let reinstall_btn_c = reinstall_btn.clone();
            let uninstall_btn_c = uninstall_btn.clone();
            let log_c = log.clone();
            let asset_c = asset.clone();
            let detected_c = detected.clone();

            uninstall_btn.on_click(move |_| {
                let Some(game) = game_from_input(&path_input_c, detected_c.as_ref()) else {
                    show_logged_error(s, &frame_c, &log_c, s.err_select_valid);
                    return;
                };
                let state = install::classify_install(&game.path);
                let InstallState::Managed(manifest) = state else {
                    show_logged_error(s, &frame_c, &log_c, s.err_uninstall_managed_only);
                    return;
                };
                let confirm = MessageDialog::builder(
                    &frame_c,
                    s.confirm_uninstall,
                    s.confirm_uninstall_title,
                )
                .with_style(MessageDialogStyle::YesNo | MessageDialogStyle::IconQuestion)
                .build()
                .show_modal();
                if confirm != ID_YES {
                    return;
                }
                if process::is_game_running() {
                    show_logged_error(s, &frame_c, &log_c, s.err_close_game_uninstall);
                    return;
                }
                match uninstall::uninstall(&game.path, &manifest) {
                    Ok(()) => {
                        log_append(&log_c, s.msg_uninstall_complete);
                        show_info(s, &frame_c, s.msg_uninstall_complete);
                    }
                    Err(e) => show_logged_error(
                        s,
                        &frame_c,
                        &log_c,
                        &fill(s.msg_uninstall_failed, &[("error", &e)]),
                    ),
                }
                refresh_state(
                    s,
                    &path_input_c,
                    detected_c.as_ref(),
                    &status_c,
                    &install_btn_c,
                    &reinstall_btn_c,
                    &uninstall_btn_c,
                    asset_c.as_ref(),
                    &log_c,
                );
            });
        }

        {
            let frame_c = frame.clone();
            close_btn.on_click(move |_| {
                frame_c.close(true);
            });
        }

        frame.show(true);
    })
    .expect("Failed to start installer UI");
}

fn game_from_input(path_input: &TextCtrl, detected: Option<&GameInstall>) -> Option<GameInstall> {
    let path = std::path::PathBuf::from(path_input.get_value());
    if !detect::validate_game_dir(&path) {
        return None;
    }
    // The input is pre-filled from auto-detection; only a path the user changed is manual.
    let source = match detected {
        Some(detected) if detected.path == path => detected.source.clone(),
        _ => GameSource::Manual,
    };
    Some(GameInstall { path, source })
}

fn refresh_state(
    s: &'static Strings,
    path_input: &TextCtrl,
    detected: Option<&GameInstall>,
    status: &StaticText,
    install_btn: &Button,
    reinstall_btn: &Button,
    uninstall_btn: &Button,
    asset: Option<&Asset>,
    log: &TextCtrl,
) {
    let Some(game) = game_from_input(path_input, detected) else {
        install_btn.enable(false);
        reinstall_btn.enable(false);
        uninstall_btn.enable(false);
        return;
    };

    let state = install::classify_install(&game.path);
    let has_asset = asset.is_some();

    match &state {
        InstallState::Fresh => {
            status.set_label(s.status_ready);
            install_btn.set_label(s.btn_install);
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::Unmanaged => {
            status.set_label(s.status_unmanaged);
            install_btn.set_label(s.btn_repair);
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::DamagedState(reason) => {
            status.set_label(s.status_damaged);
            log_append(log, &fill(s.log_damaged_state, &[("reason", reason)]));
            install_btn.set_label(s.btn_repair);
            install_btn.enable(has_asset);
            reinstall_btn.enable(false);
            uninstall_btn.enable(false);
        }
        InstallState::Managed(manifest) => {
            let update_available = match (
                Version::parse(&manifest.mod_version).ok(),
                asset
                    .and_then(|a| a.version())
                    .and_then(|v| Version::parse(&v).ok()),
            ) {
                (Some(installed), Some(latest)) => installed < latest,
                _ => has_asset,
            };
            if update_available {
                status.set_label(&fill(
                    s.status_update_available,
                    &[("version", &manifest.mod_version)],
                ));
                install_btn.set_label(s.btn_update);
                install_btn.enable(has_asset);
            } else {
                status.set_label(&fill(
                    s.status_up_to_date,
                    &[("version", &manifest.mod_version)],
                ));
                install_btn.set_label(s.btn_update);
                install_btn.enable(false);
            }
            reinstall_btn.enable(has_asset);
            uninstall_btn.enable(true);
        }
    }
}

fn install_asset(
    s: &'static Strings,
    parent: &impl WxWidget,
    game: &GameInstall,
    asset: &Asset,
    force: bool,
    releases: &[ReleaseInfo],
    log: &TextCtrl,
) {
    if process::is_game_running() {
        show_logged_error(s, parent, log, s.err_close_game_install);
        return;
    }

    let state = install::classify_install(&game.path);
    if !force {
        if let (Some(installed), Some(latest)) = (
            install::installed_version(&state),
            asset.version().and_then(|v| Version::parse(&v).ok()),
        ) {
            if installed >= latest {
                log_append(log, s.msg_already_up_to_date);
                show_info(s, parent, s.msg_already_up_to_date);
                return;
            }
        }
    }

    let temp_dir = install::temp_session_dir();
    let zip_path = temp_dir.join(&asset.name);
    log_append(log, &fill(s.log_downloading, &[("name", &asset.name)]));

    let result = (|| {
        github::download_asset(asset, &zip_path)?;
        if let Some(expected) = asset.sha256_digest() {
            log_append(log, s.log_verifying);
            install::verify_sha256(&zip_path, &expected)?;
        }
        log_append(log, s.log_installing);
        install::install_from_zip(&zip_path, &game.path, &game.source, asset, &state)?;
        Ok::<(), String>(())
    })();
    let _ = std::fs::remove_dir_all(&temp_dir);

    match result {
        Ok(()) => {
            log_append(log, s.msg_install_complete);
            log_append(log, s.msg_first_launch_note);
            for line in changelog_lines(s, &state, asset, releases) {
                log_append(log, &line);
            }
            show_info(
                s,
                parent,
                &format!("{}\n\n{}", s.msg_install_complete, s.msg_first_launch_note),
            );
        }
        Err(e) => show_logged_error(
            s,
            parent,
            log,
            &fill(s.msg_install_failed, &[("error", &e)]),
        ),
    }
}

/// The release notes of every version this install crossed, as displayable lines:
/// a header, then each version's heading and body, oldest first. Empty for a fresh
/// install (no starting version) and for a reinstall of the same version.
fn changelog_lines(
    s: &Strings,
    state_before: &InstallState,
    asset: &Asset,
    releases: &[ReleaseInfo],
) -> Vec<String> {
    let Some(from) = install::installed_version(state_before) else {
        return Vec::new();
    };
    let Some(to) = asset.version().and_then(|v| Version::parse(&v).ok()) else {
        return Vec::new();
    };
    let entries = github::changelog_between(releases, &from, &to);
    if entries.is_empty() {
        return Vec::new();
    }
    let mut lines = vec![fill(s.log_whats_new, &[("version", &from.to_string())])];
    for (version, body) in entries {
        lines.push(fill(
            s.log_whats_new_version,
            &[("version", &version.to_string())],
        ));
        lines.push(body.to_string());
    }
    lines
}

fn log_append(log: &TextCtrl, message: &str) {
    let current = log.get_value();
    if current.is_empty() {
        log.set_value(message);
    } else {
        log.set_value(&format!("{current}\n{message}"));
    }
}

fn show_info(s: &Strings, parent: &impl WxWidget, message: &str) {
    MessageDialog::builder(parent, message, s.app_title)
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconInformation)
        .build()
        .show_modal();
}

fn show_error(s: &Strings, parent: &impl WxWidget, message: &str) {
    MessageDialog::builder(parent, message, s.app_title)
        .with_style(MessageDialogStyle::OK | MessageDialogStyle::IconError)
        .build()
        .show_modal();
}

fn show_logged_error(s: &Strings, parent: &impl WxWidget, log: &TextCtrl, message: &str) {
    log_append(log, message);
    show_error(s, parent, message);
}
