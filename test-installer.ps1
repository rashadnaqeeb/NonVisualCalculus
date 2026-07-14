# Run the installer's unit tests (cargo test in installer/).

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerDir = Join-Path $scriptDir "installer"

if (-not (Test-Path (Join-Path $installerDir "Cargo.toml"))) {
    throw "Installer project not found: $installerDir"
}

if ([string]::IsNullOrWhiteSpace($env:LIBCLANG_PATH)) {
    $libclangCandidates = @(
        "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\LLVM\bin"
    )
    foreach ($candidate in $libclangCandidates) {
        if (Test-Path (Join-Path $candidate "libclang.dll")) {
            $env:LIBCLANG_PATH = $candidate
            break
        }
    }
}

# The wxWidgets build drives CMake with the Ninja generator; Visual Studio ships a
# ninja.exe that is not on PATH by default.
if ($null -eq (Get-Command ninja -ErrorAction SilentlyContinue)) {
    $ninjaCandidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja"
    )
    foreach ($candidate in $ninjaCandidates) {
        if (Test-Path (Join-Path $candidate "ninja.exe")) {
            $env:PATH = "$candidate;$env:PATH"
            break
        }
    }
}

Push-Location $installerDir
try {
    cargo test
    if ($LASTEXITCODE -ne 0) {
        throw "Installer tests failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
