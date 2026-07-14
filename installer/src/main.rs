#![windows_subsystem = "windows"]

use non_visual_calculus_installer::{cli, gui};

fn main() {
    if std::env::args().any(|a| a == "--cli") {
        attach_console();
        cli::run();
    } else {
        gui::run();
    }
}

fn attach_console() {
    #[cfg(target_os = "windows")]
    unsafe {
        windows_sys::Win32::System::Console::AttachConsole(
            windows_sys::Win32::System::Console::ATTACH_PARENT_PROCESS,
        );
    }
}
