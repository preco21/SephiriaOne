# Tabbed Settings Window Implementation Plan

> **For agentic workers:** Use subagent-driven-development or executing-plans to
> implement the checked tasks below. The user approved option 2 on 2026-10-10,
> with feature tabs, performance-conscious structure and a native Settings hotkey UI.

**Goal:** A separate translucent, draggable SephiriaOne window with feature tabs
and an optional locally configured keyboard shortcut in native Settings.

**Architecture:** Retain shared actions, immutable observations and native UIBase
lifetime. Split window/widget interaction from gameplay services. Keep hotkey
capture/storage/native Options integration separate from window rendering.

**Tech stack:** Installed Unity uGUI, TextMeshPro and Input System; C# netstandard2.1;
existing executable fixtures and native IL compatibility tests.

## Global constraints

- Each feature panel displays as a tab in one movable window; no sidebar replacement.
- Support text, inputs, buttons, sliders and real checkbox toggles.
- Preserve all existing actions, ranges, resets, authority, recovery, preset,
  session/run draft and synchronization semantics. UI refresh never executes actions.
- Keep local gameplay input gated by the native control stack while open;
  simulation continues on direct opening. Do not add a second Escape handler.
- No guest addon, custom peer protocol, gameplay writes or changes to native
  keybindings/settings from the visual redesign or hotkey configuration.
- Native Settings must expose binding and clearing of the addon keyboard hotkey
  if supported. Default is unassigned; no unsolicited default binding. Store in
  local addon UI config, separate from gameplay presets. Cancel leaves it unchanged.
- Prefer allocation-free idle input work, 0.25-second visible snapshots, changed
  text updates, event-driven dragging and bounded native lookup. Avoid per-frame
  hierarchy scans, rebuilt controls, file IO, blur shaders and external assets.
- Keep EN/KO catalogs complete; Korean remains default. Do not change the game's language.
- Work in the established clean main checkout. Build with `-p:DeployMod=false`.
  Do not deploy or run the game. Final conventional commit and push are authorized.
- Native assembly fingerprint remains the one recorded in ui-revamp-investigation.md.

## Task 1: Tabbed draggable window and reusable controls

Files: existing `SephiriaOne/UI/SettingsPanel*.cs`, `PanelWidgets.cs` and focused
new window/geometry/control helpers under `UI/`; EN/KO catalogs; relevant portable
and installed-panel compatibility tests. Do not edit gameplay feature services.

Consumes `SettingsActions.Execute`, `SessionSettings.ReadSnapshot`, `PanelDraft`
and `PanelControlLifetime`. Preserve existing `SettingsPanelController.TryOpen`.
Provide `public static bool TryToggle(out string error)` for the hotkey adapter:
close only this window when it owns current input; otherwise open only when
native controls permit it. Never close unrelated panels.

- [x] Add failing behavioral tests for window bounds/resize/center and toggle
  observation versus user intent, including failed edits and disabled-enable /
  allowed-disable behavior. Reuse lifecycle/draft tests for lost authority/run changes.
- [x] Implement pure geometry/interaction helpers and verify the regressions.
- [x] Build a readable translucent window with opaque text/control surfaces,
  title-only pointer dragging clamped to the canvas, Center and Close controls.
  Preserve position while the UI lives; rescale/reclamp on bounds changes.
- [x] Present all 13 existing feature pages as tabs, including Updates, with an
  unmistakable selected state and navigation. Use a roomier form/readout area,
  scrolling where needed. Improve selection for the 27-stat list. Retain every
  action/help/readout from the old panel and common controls for new features.
- [x] Replace paired On/Off choices with real checkbox controls where applicable.
  Use no-notify snapshot refresh and authoritative command feedback; numeric
  input/slider changes remain drafts until explicit Apply. Do not send actions
  during rebuild, localization refresh or selection changes.
- [x] Resolve the registered native root/font without requiring the pause panel.
  Keep the optional pause launcher, direct chat opening, input/focus gating and
  cleanup retry protocol. Native Options/hotkey adapter is Task 2.
- [x] Run portable/runtime regression suites, non-deploying Debug/Release builds
  and current/new installed panel contracts; document actual evidence and limits.
- [x] Review task diff for scope, stale drafts, checkbox gating and allocations.

## Task 2: Local hotkey and native Settings control

Files: focused `SephiriaOne/UI/Hotkeys/` classes, a native Options adapter,
controller hookup/Entry lifecycle as needed, Input System non-copying reference,
EN/KO catalogs and dedicated input/store/adapter tests.

Consumes the window's `TryToggle(out string error)`. Native inspection material:
`UI_OptionsPanel`, `UI_Tab`, `UI_TabContent`, `UI_TabButton`, `RebindActionUI`,
`UI_ControlRebindOverlay`, `UIInputModule` and `ControlsChangeHandler` under
`%TEMP%/sephiria-ui-revamp-research/`. Native rebind logic may modify other
bindings on conflict: do not reuse that mutation behavior.

- [x] Verify the native Options layout/insertion and installed Input System API.
  Create addon-owned controls, not a clone of native option/rebinding callbacks.
- [x] Write failing tests for unassigned/default, capture/cancel/clear, reserved
  or conflicting keys, failed persistence, no-repeat edge triggering and input
  suppression while chat/text entry, another menu or any binding capture owns input.
- [x] Implement local setting storage and a small owned keyboard binding/capture
  service. Input must not rewrite native actions, activate from the key used to
  bind it, or leak capture into opening/closing menus. Dispose capture/actions on
  teardown, rebind, exception and Options replacement. Keep idle work bounded.
- [x] Add a native Settings entry/page with current shortcut, Set/Change, Clear
  and Cancel while listening, localized feedback and persisted preference. New
  UI objects/listeners are tracked by identity and removed without erasing
  additions made by other mods. Main-menu binding may work without host gameplay.
- [x] Connect the shortcut to the same window; show useful feedback on conflicts,
  unavailable host context or adapter failure. Chat opening remains usable if
  native Options integration is unavailable. Do not choose a default hotkey.
- [x] Run covering input/storage/adapter fixtures and build/native contract checks.
- [x] Review task diff for focus, event ordering, persistence and owned cleanup.

## Task 3: Integration, documentation and delivery

- [x] Set DLL/metadata version to `0.39.0`; update UI guide, research follow-up,
  development notes, module map/index and local config/keybinding instructions.
- [x] Verify widget parity and tab coverage for Stats/Fountain/Choices/Resources/
  Presets/Status/Rabbit/Merchant/Items/Combat/Spawns/Costumes/Updates. Verify update
  and language actions remain local and no gameplay or native option storage changed.
- [x] Run all appropriate final suites, native compatibility tests, EN/KO checks,
  Debug/Release builds with deployment off, diff/line-ending/doc-link checks.
- [x] Independent final review; fix actionable findings and rerun covering checks.
- [x] Record live limitations (Unity focus/rendering, drag, keyboard capture and
  actual native-menu layout); do not claim fixture results as live verification.
- Final delivery: conventional commit, push main and verify remote synchronization/clean tree; the resulting commit is recorded in Git history.

## Progress

- Baseline: clean `main` at `ca99eb0`; user approved implementation in place.
- No deployment, game execution or native settings changes are authorized by this plan.

- Task 1 independently approved after separating optional launcher replacement from window teardown.
- Task 2 and integrated change independently approved; final verification is recorded in docs/tabbed-settings-ui.md.
- Non-deploying addon version0.39.0; no game launch. Commit/push follows these checks.
