---
name: release
description: Cut and publish a mod release end to end - set the version, promote the changelog's Unreleased section, test, build the zip and installer, commit, tag, push, publish the GitHub release, verify it against the installer and update-check contract, delete the staged artifacts, redeploy the Debug build to the local game. Use when asked to release version X.Y.Z. Not for rebuilding artifacts without publishing - that is build_release.ps1 / build-installer.ps1 directly.
argument-hint: <version>
---

Publish the release whose version is given as the argument. The argument is a bare three-part version like `1.2.0`; strip a leading `v` if one was typed. Work through the phases in order; each is a gate for the next, and everything before Phase 4 leaves nothing public, so a failure there just stops.

## Phase 0 - preconditions

- The version must match `\d+\.\d+\.\d+` exactly. This is a hard contract, not a style rule: the installer identifies the mod zip asset by the name pattern `NonVisualCalculus-v<maj>.<min>.<patch>.zip` and parses that version with the semver crate (`installer/src/core/github.rs`, `paths.rs`), and the mod's launch update check parses the release tag with `System.Version` (`src/NonVisualCalculus.Core/Updates/UpdateCheck.cs`). A two-part or suffixed version publishes a release the installer cannot consume, and because the installer always reads `releases/latest`, that breaks every new install immediately, not just the new version.
- The version must be strictly greater than the newest existing `v*` tag (semver order; no tags at all is fine). An equal or lower version makes the update check silently stop announcing to players on newer builds.
- On `main`, working tree clean, `main` not behind `origin/main` (`git fetch origin` and compare), and `gh auth status` succeeds.
- The game may stay running until the final Debug redeploy in Phase 5: everything else builds Release, which never deploys, so no game DLL is touched before then.

## Phase 1 - version and changelog

- Set `<Version>` in `Directory.Build.props` to the target (skip the edit if already set). This is the single version source: `build_release.ps1` names the zip from it, and the same value is compiled into `BuildVersion.Value`, which is what the running mod compares against the release tag.
- `CHANGELOG.md`: if a `## V<version>` section already exists with content, use it as is. Otherwise retitle the `## Unreleased` section to `## V<version>`. The retitle is the only changelog edit that never needs approval; every change to entry text does. Check `git log v<previous>..HEAD` for player-facing changes the section is missing, but never add, reword, delete, or draft entries without showing the user the exact proposed text and getting their approval first: this text becomes the published GitHub release notes verbatim.
- Leave a fresh empty `## Unreleased` heading at the top so future work has its standing slot.
- The heading format is `## V<version>` with a capital V. `create-release.ps1` extracts the notes by finding exactly that heading and fails on a missing or empty section.

## Phase 2 - test and build

- `dotnet test NonVisualCalculus.slnx` must be green.
- `powershell.exe -NoProfile -File build_release.ps1` then `powershell.exe -NoProfile -File build-installer.ps1`, producing `releases\NonVisualCalculus-v<version>.zip`, `releases\WhirlingInWords-v<version>.zip` (the compat zip: the same release under the mod's pre-rename asset name, which is the only asset name a Whirling in Words-era installer exe recognizes; it carries zero-byte tombstones over the old plugin folder so that installer's overwrite-only update still kills the old mod), and `releases\NonVisualCalculusInstaller.exe`. The project settings allow exactly these invocations, run from the repo root: no `cd &&` or environment-variable prefix, or the permission rule won't match.
- If the scripts still cannot be run (the allow rules were removed, or a different environment), perform each script's steps directly with allowed tools instead, reading the script first so the replication stays faithful: for the zip, `dotnet build -c Release`, stage the BepInEx zip contents plus plugin DLLs, audio assets, lang files, and prism.dll exactly as build_release.ps1 lays them out, and zip with clean forward-slash relative paths (python zipfile works); for the installer, probe LIBCLANG_PATH and ninja as the script does, `cargo build --release`, and copy the exe. Verify the zip afterward against the installer's `required_loader_files()` list.
- Build before committing or tagging, so a failed build pushes nothing. At this point the tree holds exactly the release edits, so the artifacts match the commit about to be made.

## Phase 3 - commit, tag, push

- One commit with the version and changelog edits (plus anything else this release session prepared), message `Release <version>`. If nothing changed (a re-run), skip the commit.
- `git tag v<version>`, then `git push origin main v<version>`.

## Phase 4 - publish

- `powershell.exe -NoProfile -File create-release.ps1 v<version>` (same allowed invocation form as Phase 2). It re-verifies the tag exists locally and on origin, finds both artifacts, extracts the changelog section, and runs `gh release create` with the zip and installer attached.
- If PowerShell is blocked (same as Phase 2), replicate: extract the lines between `## V<version>` and the next `##` heading into a notes file, then `gh release create v<version> <zip> <compat zip> <installer.exe> --title "V<version>" --notes-file <notes>`.

## Phase 5 - verify and clean up

- Read back `gh api repos/rashadnaqeeb/NonVisualCalculus/releases/latest` and confirm the consumer contracts on the real thing: `tag_name` is `v<version>` (what the update check announces from), and the assets are `NonVisualCalculus-v<version>.zip` (what the installer's name pattern must match), `WhirlingInWords-v<version>.zip` (what a pre-rename installer's name pattern must match), and `NonVisualCalculusInstaller.exe`.
- Delete `releases\NonVisualCalculus-v<version>.zip`, `releases\WhirlingInWords-v<version>.zip`, and `releases\NonVisualCalculusInstaller.exe`. `create-release.ps1` only checks that the files exist, so an artifact left behind can be republished stale by a future run that skipped a build.
- Finish with `dotnet build NonVisualCalculus.slnx -c Debug` so the deployed local game build carries the released version (only Debug deploys, so until this runs the game still loads the pre-bump build and its update check compares against the wrong version). If the running game holds the DLLs locked and the deploy is skipped (`MSB3021`), carry the cycle per CLAUDE.md: close the game, rebuild, relaunch through Steam.

## Failure and re-run notes

- Every phase is re-runnable. If publish fails after the tag was pushed, fix the cause and re-run `create-release.ps1` - the tag and artifacts are still valid.
- If the release was created wrong, `gh release delete v<version>` removes the release and its assets but keeps the tag; re-run Phase 4. Delete and re-push the tag only if the tagged commit itself is wrong.
- Do not edit a published release's zip by re-uploading under the same name unless the version is bumped: installs in the wild key on the asset digest and version name, and a silently different artifact under an already-announced version is exactly the stale-data failure this mod exists to avoid.
