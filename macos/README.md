# Non-Visual Calculus on macOS

The mod runs on the Mac version of Disco Elysium: The Final Cut from Steam, on an Apple Silicon Mac, with the game running natively (Rosetta is neither needed nor used). Everything in the main README applies; this file covers what is different on a Mac.

## Installing

1. Download the macOS release zip (`NonVisualCalculus-macOS-v<version>.zip`), unzip it anywhere, and in Terminal run `./install.sh` from the unzipped folder. It copies the mod loader, the mod and its launcher into the game folder (the one containing `disco.app`), clears the quarantine flag macOS puts on downloaded libraries, and puts the exact launch option line on the clipboard (and prints it). If the game is not in Steam's default library, pass the game folder as the argument: `./install.sh "/path/to/Disco Elysium"`.
2. In Steam, open the game's Properties, General, Launch Options, and paste the line from the clipboard: `"<game folder>/NonVisualCalculus.app" %command%`, quotes included.
3. Launch from Steam. The first modded launch prepares the mod for your copy of the game, which takes a minute or two with a black screen. After that the mod announces itself at the main menu.

To update, unzip the new release and run its `install.sh` again; your settings are kept. To play without the mod, clear the launch option.

## Speech

The mod picks its voice at startup:

- **Your screen reader, if it supports Prism.** A screen reader that ships a Prism plugin (linked under `~/Library/Application Support/<reader>/prism/`) speaks for the mod while it runs, with its own voice, rate and settings, exactly as NVDA does on Windows.
- **Otherwise the system voice.** The mod speaks the macOS system voice itself, with the voice and speaking rate set under System Settings, Accessibility, Spoken Content. Lines queue with no gap between them, and any key press cuts speech off, as a screen reader would.
- **VoiceOver, if you ask for it.** Turn on "Speak through VoiceOver" in the mod settings (F12) to speak through VoiceOver while it is running. VoiceOver cannot queue speech, so one line can cut off the next; this is why it is not the default. The mod never starts VoiceOver itself.

The log (`BepInEx/LogOutput.log` in the game folder) names the voice in use on the line starting `Speech:`.

## Keys

The keys are the same as on Windows. Ctrl is the Control key, Alt is the Option key, and Shift is Shift; the key help (Shift+F1) says Option where the Windows build says Alt.

## How it works

The Mac game is an IL2CPP build, which BepInEx does not officially support on macOS. The mod ships its own build of the loader (BepInEx 6 with Il2CppInterop, HarmonyX, Dobby and UnityDoorstop, patched to run natively on arm64) plus the .NET 6 runtime for Apple Silicon. Steam's launch option starts `NonVisualCalculus.app`, which runs `run-modded.sh` beside it; that script opens the game's own bundle as its arm64 slice with the loader's environment. The header of `run-modded.sh` explains the details.

Logs: `BepInEx/LogOutput.log` in the game folder for the mod. Saves are in `~/Library/Application Support/ZAUM Studio/Disco Elysium/`.

## Building from source (developers)

On an Apple Silicon Mac, with the [.NET SDK](https://dotnet.microsoft.com/download) and Steam's copy of the game:

1. Run `macos/setup-bepinex.sh`. It installs the loader from `third_party/bepinex-macos` and the launcher beside `disco.app`.
2. Start the game with `run-modded.sh` in the game folder (Steam must be running) and wait for the main menu; the first launch generates the files the build compiles against. Quit.
3. Run `dotnet build NonVisualCalculus.slnx`. It builds the mod and deploys it into the game folder.

`build_release.sh` builds the release zip. `macos/loader/build-loader.sh` rebuilds the loader zip from pinned upstream sources and the patches in `macos/loader/patches/`.
