# BepInEx for macOS (vendored loader, Apple Silicon)

`BepInEx-Unity.IL2CPP-macos-arm64-nvc.zip` is the mod loader for the Mac version of the game, built by
`macos/loader/build-loader.sh` from pinned upstream sources (BepInEx 6 bleeding edge, Il2CppInterop,
Dobby, UnityDoorstop) plus the patches in `macos/loader/patches/`, with the official .NET 6.0.36
runtime for osx-arm64. Upstream BepInEx does not support IL2CPP on macOS; the script's header lists
what each patch changes. Licenses for every component are inside the zip under `BepInEx/licenses/`.

The zip root is the game folder (the one containing `disco.app`): `libdoorstop.dylib`, `dotnet/` and
`BepInEx/`. `macos/setup-bepinex.sh` installs it for development; `build_release.sh` folds it into
the macOS release zip.

To update: change the pinned commits or the patches, run `macos/loader/build-loader.sh`, and commit
the rebuilt zip.
