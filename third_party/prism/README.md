# Prism (vendored)

Native screen-reader / TTS abstraction. Source: https://github.com/ethindp/prism
License: MPL-2.0 (see `LICENSES/prism__mpl-2.0.txt`; bundled-dependency licenses and `NOTICE` alongside).

**Version: v0.16.7**, Windows x64, dynamic release build (`prism-windows-x64.zip`).

## Files
- `prism.dll` - the runtime library, deployed next to `disco.exe`. Imports only stock Windows DLLs (kernel32, ole32, oleaut32, user32, UIAutomationCore, ...); it talks to NVDA/JAWS/SAPI itself.
- `include/prism.h` - C header. Reference for the hand-written P/Invoke layer that binds `prism.dll` directly (the WOTR way; framework-agnostic, matches our .NET 6 / IL2CPP host).

## Consuming it
Mirror WOTR's `src/Speech/PrismNative.cs`: `[DllImport("prism", EntryPoint="prism_...")]`. Core flow: `prism_init` -> `prism_registry_create_best` -> `prism_backend_initialize` -> `prism_backend_output(text, interrupt)` / `prism_backend_stop` -> `prism_backend_free` -> `prism_shutdown`.

## macOS
`macos/` holds **v0.18.2**, macOS universal, dynamic release build (`prism-macos-universal.zip`):
`libprism.dylib` (deployed beside the plugin), its `include/` headers, `LICENSES/` and `NOTICE`. The
Mac speech backend needs 0.18's registry builder to load screen readers' Prism plugins. The same
P/Invoke layer binds both libraries; the 0.18-only calls are made on macOS alone.

## Updating
Download a new `prism-windows-x64.zip` from the releases page; copy `dynamic/release/bin/prism.dll`, `include/prism.h`, and `LICENSES/` + `NOTICE` here. Bump the version above.
