# Host settings panel implementation plan

> For agentic workers: use subagent-driven development for the shared controls
> task, then review the complete integrated feature. The user asked to continue
> the recommended design from `control-panel-investigation.md`.

**Goal:** An in-session host panel for existing Fountain, choices, stats and preset
commands, reached from the pause menu or `/mod ui`, with live read-only status.

**Architecture:** Both chat and UI call a validated action dispatcher; session
code exposes an immutable snapshot. A code-built uGUI/TMP `UIBase` participates in
native focus/cancel handling. All gameplay effects keep the existing services.

**Constraints:** No hotkeys without asking the user. Mouse/keyboard first. Name
gradient is read-only. No deployment; every addon build uses `-p:DeployMod=false`.
Preserve native replication for unmodified guests, current commands/preset format,
fault recovery, cumulative relative stats and native baseline resets. Keep C# LF.

## Task 1: Shared actions and snapshots

- [x] Add `Controls/SettingsActions.cs` with `Execute(string command)` returning
  `SettingsActionResult` (`Recognized`, `Success`, `OpenPanel`, `Messages`).
  Delegate to current parsers/services; help/list remain local, `/mod ui` is
  host-only, unknown chat remains unrecognized. Catch command exceptions once.
- [x] Add immutable `SettingsSnapshot`/`PlayerSettingsSnapshot` DTOs under
  `Controls/` and `SessionSettings.ReadSnapshot(bool refreshSaved = false)` in
  `Session/SessionSettingsSnapshot.cs`. Expose host readiness, session object,
  epoch/run/revision, fault family, saved validity, typed player values and
  formatted status lines. Reading never calls Prepare/Synchronize or writes.
- [x] Share snapshot formatting with `/mod status`. Cache saved-preset reads
  between explicit refresh/open/save/forget; invalidate on controller lifecycle.
  Keep existing status semantics and strings used by regression tests.
- [x] Add runtime regressions for dispatcher parity, rejected guests/invalid
  input, `/mod ui`, read-only pending joins, immutable snapshots, saved-cache
  refresh, native value changes, fault recovery, and old-session identity.

Exact integration contract:

```csharp
SettingsActionResult SettingsActions.Execute(string command);
SettingsSnapshot SessionSettings.ReadSnapshot(bool refreshSaved = false);
// Result: bool Recognized, Success, OpenPanel; IReadOnlyList<string> Messages.
// Snapshot: object SessionIdentity; long Epoch, RunGeneration, Revision;
// bool HostActive, CanMutate, CanSave, CanForget, ChoicesAvailable, SavedValid;
// string AvailabilityReason, FaultedFeature, SavedSummary;
// IReadOnlyList<string> Lines, ActiveSettings, SavedSettings;
// IReadOnlyList<PlayerSettingsSnapshot> Players;
// Player: uint Id; string Name; int FountainPoints, FountainContribution;
// IReadOnlyDictionary<string, decimal> Stats (StatCatalog names, displayed units);
// IReadOnlyDictionary<string, int> ExtraChoices (item/weapon/miracle).
```

## Task 2: Native panel and entry points

- [x] Audit pause-menu asset bounds; create a fresh addon-owned button without
  copying native listeners. Rebind on UI-manager/pause-panel replacement.
- [x] Add `UI/SettingsPanelController.cs`, `UI/SettingsPanel.cs` and focused UI
  construction helpers. Use an existing root/font/EventSystem, a responsive
  panel, clear scope heading, tabs/rows and explicit action buttons.
- [x] Refresh snapshots while open using unscaled time. Preserve typed drafts;
  clear on scope/run replacement. Check scope at click before executing a draft.
  Re-render never dispatches actions. Show per-player current values separately.
- [x] Keep family-wide recovery resets available during partial-write faults;
  service checks remain authoritative. Clear successful delta input. Save acts
  only on applied intent; explain that drafts are excluded.
- [x] Route chat through the shared dispatcher and invoke the controller for
  `/mod ui`. Keep unknown chat and native submission behavior intact.
- [x] Own UI stack lifetime explicitly: close before disposal, prevent repeated
  registration, disable interaction during transitions/authority loss, and
  preserve existing failed-unload recovery ordering. Do not change timeScale.
- [x] Add meaningful portable draft/lifetime tests and installed-native API/IL
  compatibility checks; build to validate actual Unity API types.

## Task 3: Verification and delivery

- [x] Run portable and runtime Release tests; inspect all failures and fix them.
- [x] Run Debug and Release addon builds with deployment disabled, then installed
  native compatibility checks against Release output.
- [x] Review UI lifecycle and controls/snapshot changes independently and resolve
  actionable findings. Record live-rendering limitations honestly.
- [x] Update version to `0.13.0`, docs/status and usage. Verify links, C# LF,
  whitespace and unchanged installed addon hashes.

Delivery workflow: commit with Conventional Commits, integrate into main, push
and verify remote HEAD. Do not run `Deploy-Mod.ps1`; Git history records delivery.

Baseline: 639 portable plus 221 runtime checks pass (860 total). Native UI and
live multiplayer behavior require in-game verification after manual installation.

## Review findings resolved

- Native `Open`/`Close` callbacks can throw after changing stack membership.
  The initial boolean flag could leak an entry or lose the chance to retry.
  `PanelControlLifetime` now observes reference identity in the retained manager's
  real stack; disposal waits for removal, including cleanup-only retries after
  addon unload. Failure tests cover callbacks before/after insertion/removal,
  duplicate opening/closing, equal-but-distinct controls and vanished scopes.
- Missing pause panels are tolerated while binding native UI. Fresh controls
  own their listeners; no native Options panel or input-binding save callbacks
  are cloned.
- An unavailable Choices guard now has an explicit reason on its tab while
  recovery resets remain available. Presets show separate active/saved summaries
  from the snapshot instead of burying saved intent in player diagnostics.

The cleanup protocol cannot repair an arbitrary exception inside the native
manager between its counter update and stack insertion/removal. It deliberately
does not edit private counters or foreign controls. Native happy-path APIs and
the callback-boundary recovery cases are checked; Unity event-loop and live
network behavior remain subject to the [manual checklist](control-panel.md#live-verification-still-required).

## Verification record (2026-09-24)

- Debug and Release addon builds succeeded with zero warnings/errors and
  `-p:DeployMod=false`; assembly and metadata versions are `0.13.0`.
- Portable Release checks: **665 passed**, including 10 draft-isolation and 16
  control-lifetime checks. Runtime command/session fixtures: **316 passed**,
  including 95 new shared-action/snapshot/preset-view checks. **981 total**.
- Installed-native inspection passed for the UI stack/cancel APIs, shared
  dispatcher calls, tested membership protocol, disposal/unload ordering, nine
  lifecycle paths, name transport, both candidate guards and embedded dependency.
- C# LF and whitespace checks passed; local documentation links resolve.
- Installed addon DLL and metadata SHA-256 stayed unchanged:

  ```text
  DLL       34C1A73E7F97670833CC71E2A9967096F267030DB49D9E1C8FD0FFC5C3ED4EEF
  metadata  DC05E1DD791F4EE071EB78ED03B839455D825A0A08D4939313CB9B6096BD7B72
  ```

Commands used from the repository:

```powershell
dotnet build SephiriaOne.slnx -c Debug --nologo -p:DeployMod=false
dotnet build SephiriaOne.slnx -c Release --nologo -p:DeployMod=false
dotnet run --project tests/SephiriaOne.RuntimeTests -c Release
dotnet run --project tests/SephiriaOne.Tests -c Release -- 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed' '.\SephiriaOne\bin\Release\netstandard2.1\SephiriaOne.dll'
```

No game execution, deployment, hotkey binding or live multiplayer test occurred.
