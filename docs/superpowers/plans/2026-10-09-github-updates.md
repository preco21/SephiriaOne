# GitHub Updates Implementation Plan

> Execute the approved update design with test-driven development and focused
> subagent review. User authorized implementation with both command and button.

**Goal:** Automatically announce releases; install only on an explicit command or
button action, and activate the selected version after a full restart.

**Architecture:** A local Updates service owns release discovery, validation and
installation. A Unity adapter publishes status on the main thread. Chat and panel
actions share that service, outside session synchronization and presets.

**Tech Stack:** netstandard2.1, existing Newtonsoft.Json, HttpClient, ZipArchive,
SHA-256, atomic File.Replace; portable net10.0 tests and installed-game checks.

## Global constraints

- Repository: C:/Users/preco/repos/SephiriaOne. Keep existing gameplay unchanged.
- Never deploy or publish a release. All build/test commands use DeployMod=false.
- Fixed public source preco21/SephiriaOne, stable releases and SephiriaOne.zip only.
- Both `/one update install` and an Update button use the same explicit action.
- Full restart required; no loaded-DLL overwrite, hot reload or peer messages.
- Preserve presets, translations and unrelated files; reject downgrades.
- English/Korean messages; local automatic checks can be disabled.
- Conventional commit and push after verification, as authorized in AGENTS.md.

## Task 1: Release client and installation transaction

Files: new `SephiriaOne/Updates/UpdateRelease.cs`, `GitHubReleaseClient.cs`,
`UpdateInstaller.cs`; new `tests/SephiriaOne.UpdateTests` portable project.

Interfaces (all internal, namespace SephiriaOne):

```csharp
// Immutable release validated by the client.
UpdateRelease.Version // System.Version, normalized three-part stable version
UpdateRelease.Tag // string
UpdateRelease.DownloadUrl // string
UpdateRelease.Sha256 // string, lowercase digest without prefix
UpdateRelease.Size // long
Task<UpdateRelease> GitHubReleaseClient.CheckAsync(CancellationToken token);
Task<byte[]> GitHubReleaseClient.DownloadAsync(UpdateRelease release, CancellationToken token);
// IDisposable client, fixed public repository; null check result means no stable release.
UpdateInstaller(string addonFolder, string runningDllPath);
Version UpdateInstaller.ReadInstalledVersion();
void UpdateInstaller.Install(UpdateRelease release, byte[] zip, CancellationToken token);
```

- [x] Write failing fixtures for stable ordering/asset validation, HTTP failures,
  bounded downloads, ZIP hash/entry validation and no install on rejection.
- [x] Verify failure, implement client and installer, then verify passing tests.
- [x] Test immutable version DLL staging and single-file metadata commit in temp
  directories, old DLL retention, cancellation, locked metadata, repeat install,
  version conflicts and path/reparse protection. Tests never write to the game.
- [x] Review the task diff for archive/HTTP trust boundaries and transaction gaps.

Use bounded responses (JSON 256 KiB, ZIP 16 MiB, expanded DLL 32 MiB, metadata
64 KiB). Require exactly the two known root files. Compare tag, metadata and
assembly name/version without loading downloaded code. Before commit re-read
installed metadata and reject concurrent/manual changes and older/equal versions.
Persist a previous metadata backup inside the addon folder under a unique
`.previous.json` filename (the loader reads only `metadata.json` there); never
create another discoverable addon folder. No DLL garbage
collection in this first release: retaining old versions is safer than guessing
ownership. A manual rollback procedure will be documented.

## Task 2: Local coordinator and adapter

Files: new `Updates/UpdateCoordinator.cs`, `UpdateFeature.cs`, `UpdateController.cs`,
`UpdateCommand.cs`, `UpdateText.cs`; modify Entry and SettingsActions.

- [x] Write failing coordinator tests using controlled async client/installer
  delegates, no real network: one operation, no implicit download, compare against
  running/installed versions, cancellation/disposal and failed checks preserving
  usable state. Exercise startup scheduling and cooldown across restarts.
- [x] Implement state snapshots, local config/cache, automatic startup checks and
  explicit check/install actions. Allow manual checks subject to short throttle
  and respect server rate-limit delays; no continuous polling.
- [x] Commands: `/one update check|status|install`, `/one update auto on|off`.
  Recognize before gameplay authority checks. Local preference persistence stays
  outside SessionSettings and session-preset files.
- [x] Initialize after localization; disable/cancel before addon teardown. One
  lightweight controller observes async task completion and delivers deduplicated
  local GameLogWriter notices once its UI exists. No Unity API on worker threads.

## Task 3: Update button, localization, release documentation

Files: modify `UI/SettingsPanel.cs`; new `UI/SettingsPanelUpdates.cs`; catalogs;
csproj/metadata version 0.38.0; `scripts/Package-Mod.ps1`; `docs/github-updates.md`.

- [x] Test both command and button routes, host-state independence of the local
  command, notices and localization keys. Keep gameplay buttons host-only.
- [x] Add a visible update indicator/button to existing panel chrome, opening an
  Updates view with status, Check, Update and automatic-check On/Off controls.
  Do not expand/overlap the existing two rows of gameplay tabs.
- [x] Add complete en/ko messages and formatting tests. Show running and installed
  versions, availability, progress/errors, restart required and manual fallback.
- [x] Add packaging script producing only root DLL and metadata, matching release
  versions, never invoking Deploy-Mod. Document first manual installation, update
  commands/button, rollback, failure behavior and test limits.
- [x] Run updater, localization, runtime and portable/native compatibility suites;
  Release/Debug builds with deployment disabled. Review final diff and commit/push.

## Progress

Plan checked against the investigation and the user's choice. No additional
approval or hotkey selection is needed. Runtime update smoke testing will be
reported separately from deterministic filesystem/HTTP/fixture checks.

Completed on 2026-10-09: core 78 checks, coordinator/commands 53, runtime 1255
plus 69 Bat checks, 2570 catalog checks, localization loader 50, full portable and
installed-game contracts, Debug/Release builds. Live public release discovery and
real package installation over the old release passed outside the game in a
temporary fixture. Review found and fixed failure cleanup deleting a DLL selected
by concurrent metadata; regression reproduced before correction. A packaging
retry also caught PowerShell's null-string binding and was corrected/retested.
Final review caught case-insensitive preference rebinding; exact field-name and
Boolean validation now rejects malformed/conflicting automatic-check settings.
Actual Unity TLS/rendering/restart activation remains a live smoke-test limit.
