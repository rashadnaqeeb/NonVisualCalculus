#!/bin/bash
# Builds the macOS (Apple Silicon) mod loader from pinned upstream sources plus the patches in
# patches/, and zips it to third_party/bepinex-macos/. Run on an Apple Silicon Mac with the .NET SDK,
# Xcode's command line tools, cmake and xmake (brew install cmake xmake). Network access is needed
# for the sources, NuGet packages and the .NET runtime.
#
#   macos/loader/build-loader.sh
#
# What goes in the zip, laid out to unzip beside disco.app:
#   libdoorstop.dylib   UnityDoorstop, inserted by dyld; hooks il2cpp_init and boots .NET
#   dotnet/             the official .NET 6 runtime for osx-arm64 (BepInEx's own runtime build has
#                       no arm64 macOS flavour), plus the Microsoft.Extensions assemblies BepInEx's
#                       runtime bundle carries
#   BepInEx/core/       BepInEx 6 (IL2CPP) with Il2CppInterop, HarmonyX, MonoMod, Cpp2IL, and
#                       libdobby.dylib for native detours
#
# BepInEx does not support IL2CPP on macOS upstream; the patches are what make it run natively on arm64:
#   bepinex.patch        finding the game data and GameAssembly.dylib inside the app bundle, writing
#                        MonoMod detours through Doorstop's doorstop_jit_memcpy (MAP_JIT memory
#                        refuses mprotect on arm64), detecting ARM on CoreCLR (all three after
#                        cdobbyn's work in BepInEx PRs 1407 and 1409), following arm64 export thunks
#                        before a native detour, skipping the x86-only method reference scan, and
#                        building against the patched Il2CppInterop
#   il2cppinterop.patch  locating GameAssembly through dladdr (Process.Modules lists only the main
#                        executable on macOS), an arm64 jump-target scanner bounded by the image's
#                        LC_FUNCTION_STARTS, and the arm64 resolution of the class-injection hooks
#   dobby.patch          building Dobby for arm64 macOS rather than iOS
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
work="${NVC_LOADER_WORK:-$repo/obj/macos-loader}"
out_dir="$repo/third_party/bepinex-macos"
out_zip="$out_dir/BepInEx-Unity.IL2CPP-macos-arm64-nvc.zip"
windows_zip="$repo/third_party/bepinex/BepInEx-Unity.IL2CPP-win-x64-6.0.0-pre.2.zip"

bepinex_commit=5b766a3b7f6c164d4798924a93f3acf4db769d06       # BepInEx/BepInEx master
il2cppinterop_commit=81a6f78c8b653e0da4a3420ac4cd00819e8b6292 # BepInEx/Il2CppInterop master
dobby_commit=9d271803881f467aabfc24593c7dcaeb710fe861         # BepInEx/Dobby master
doorstop_commit=3035af7ae73ed071200c995886264d62ceb3488b      # NeighTools/UnityDoorstop master (has doorstop_jit_memcpy)
dotnet_version=6.0.36
dotnet_sha512=d13fa060270c376307c7086da52cff71a02f8550a3a21812e25127308970c99a32d3c4834e342f23365869600c0b51fce03236906e13fe76456fc78964c531d4
interop_version=1.5.4-nvc.1

if [ "$(uname -m)" != arm64 ]; then
    echo "Build the loader on an Apple Silicon Mac." >&2
    exit 1
fi
for tool in dotnet cmake xmake clang git curl shasum; do
    command -v "$tool" >/dev/null || { echo "missing tool: $tool" >&2; exit 1; }
done

fetch() { # url dir commit
    if [ ! -d "$2/.git" ]; then
        git init -q "$2"
        git -C "$2" remote add origin "$1"
    fi
    git -C "$2" fetch -q --depth 1 origin "$3"
    git -C "$2" checkout -q --force "$3"
    git -C "$2" clean -qfdx
}

mkdir -p "$work"
echo "== sources"
fetch https://github.com/BepInEx/BepInEx.git "$work/BepInEx" "$bepinex_commit"
fetch https://github.com/BepInEx/Il2CppInterop.git "$work/Il2CppInterop" "$il2cppinterop_commit"
fetch https://github.com/BepInEx/Dobby.git "$work/Dobby" "$dobby_commit"
fetch https://github.com/NeighTools/UnityDoorstop.git "$work/UnityDoorstop" "$doorstop_commit"
git -C "$work/BepInEx" apply "$here/patches/bepinex.patch"
git -C "$work/Il2CppInterop" apply "$here/patches/il2cppinterop.patch"
git -C "$work/Dobby" apply "$here/patches/dobby.patch"

echo "== Il2CppInterop"
rm -rf "$work/feed"
mkdir -p "$work/feed"
for project in Il2CppInterop.Common Il2CppInterop.Runtime Il2CppInterop.HarmonySupport Il2CppInterop.Generator; do
    dotnet build "$work/Il2CppInterop/$project/$project.csproj" -c Release -p:Version="$interop_version" -v quiet -nologo
    dotnet pack "$work/Il2CppInterop/$project/$project.csproj" --no-build -c Release -p:Version="$interop_version" -o "$work/feed" -v quiet -nologo
done
# The patched version must come from the feed, never a stale copy in the NuGet cache.
rm -rf "$HOME/.nuget/packages/il2cppinterop."{common,runtime,harmonysupport,generator}"/$interop_version"

echo "== BepInEx"
dotnet build "$work/BepInEx/Runtimes/Unity/BepInEx.Unity.IL2CPP/BepInEx.Unity.IL2CPP.csproj" \
    -c Release -p:GeneratePackageOnBuild=false -v quiet -nologo
bepinex_out="$work/BepInEx/Runtimes/Unity/BepInEx.Unity.IL2CPP/bin/Unity.IL2CPP"

echo "== Dobby"
cmake -S "$work/Dobby" -B "$work/Dobby/build-arm64" -DCMAKE_SYSTEM_PROCESSOR=arm64 -DCMAKE_SYSTEM_NAME=Darwin \
    -DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_BUILD_TYPE=Release -DDynamicBinaryInstrument=OFF \
    -DPlugin.SymbolResolver=OFF -DCMAKE_POLICY_VERSION_MINIMUM=3.5 >/dev/null
make -C "$work/Dobby/build-arm64" -j8 dobby >/dev/null

echo "== UnityDoorstop"
(cd "$work/UnityDoorstop" && xmake f -p macosx -a arm64 -m release -y >/dev/null && xmake build -y doorstop_arm64 >/dev/null)

echo "== .NET runtime"
dotnet_tgz="$work/dotnet-runtime-$dotnet_version-osx-arm64.tar.gz"
if [ ! -f "$dotnet_tgz" ]; then
    curl -fsSL -o "$dotnet_tgz" \
        "https://builds.dotnet.microsoft.com/dotnet/Runtime/$dotnet_version/dotnet-runtime-$dotnet_version-osx-arm64.tar.gz"
fi
echo "$dotnet_sha512  $dotnet_tgz" | shasum -a 512 -c - >/dev/null
rm -rf "$work/dotnet-extract"
mkdir -p "$work/dotnet-extract"
tar -xzf "$dotnet_tgz" -C "$work/dotnet-extract"

echo "== stage"
stage="$work/stage"
rm -rf "$stage"
mkdir -p "$stage/BepInEx/core" "$stage/dotnet" "$stage/BepInEx/licenses"
rsync -a --exclude runtimes --exclude '*.xml' "$bepinex_out/" "$stage/BepInEx/core/"
cp "$work/Dobby/build-arm64/libdobby.dylib" "$stage/BepInEx/core/"
cp "$work/UnityDoorstop/build/macosx/arm64/release/libdoorstop.dylib" "$stage/"
rsync -a "$work/dotnet-extract/shared/Microsoft.NETCore.App/$dotnet_version/" "$stage/dotnet/"
# The managed Microsoft.Extensions assemblies BepInEx's runtime bundle ships; platform-neutral IL,
# taken from the vendored Windows bundle so both platforms run the same ones.
unzip -q -o -j "$windows_zip" 'dotnet/Microsoft.Extensions.*.dll' 'dotnet/Microsoft.Bcl.AsyncInterfaces.dll' -d "$stage/dotnet"
# That zip records no permissions for its entries.
chmod 644 "$stage/dotnet/"Microsoft.Extensions.*.dll "$stage/dotnet/Microsoft.Bcl.AsyncInterfaces.dll"
cp "$work/BepInEx/LICENSE" "$stage/BepInEx/licenses/BepInEx-LICENSE.txt"
cp "$work/Il2CppInterop/LICENSE" "$stage/BepInEx/licenses/Il2CppInterop-LICENSE.txt"
cp "$work/Dobby/LICENSE" "$stage/BepInEx/licenses/Dobby-LICENSE.txt"
cp "$work/UnityDoorstop/LICENSE" "$stage/BepInEx/licenses/UnityDoorstop-LICENSE.txt"
cp "$work/dotnet-extract/LICENSE.txt" "$stage/BepInEx/licenses/dotnet-LICENSE.txt"
cp "$work/dotnet-extract/ThirdPartyNotices.txt" "$stage/BepInEx/licenses/dotnet-ThirdPartyNotices.txt"

mkdir -p "$out_dir"
rm -f "$out_zip"
(cd "$stage" && zip -qry "$out_zip" .)
echo "Built $out_zip"
