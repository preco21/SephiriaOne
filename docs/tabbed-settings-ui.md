# Tabbed settings window and local hotkey

Introduced in `0.39.0`, following the user's approval of option 2 from the
[UI comparison](ui-revamp-investigation.md). The window uses the installed
Unity uGUI/TextMeshPro components and the existing settings services.

## Window

Open with `/one ui`, the **SephiriaOne** pause-menu button, or your configured
shortcut. The window is separate from the pause menu. Drag its title bar to
move it; **Center** restores its position. It clamps to the current canvas and
scales down for smaller screens. Its position lasts for the current UI instance.

All existing pages are tabs: **Stats, Fountain, Choices, Resources, Presets,
Status, Rabbit, Merchant, Items, Combat, Spawns, Costumes and Updates**. Stats
also has a scrollable selector for the 27 supported visible stats. Boolean
settings use checkboxes. Text and slider edits remain drafts until their action
button is pressed. Failed checkbox actions restore the observed state.

The panel still enters the native input-control stack. Direct opening does not
pause simulation. Native Escape/cancel closes it; closing restores the previous
native control. Session/run changes clear drafts. Host authority, compatibility
checks and recovery resets use the same shared actions as chat.

## Native Settings integration

The local shortcut belongs in the game's **Settings → Keyboard controls** list.
It starts **unassigned**. It is independent of gameplay presets and native
keybinding storage. Binding the shortcut does not change the game's controls.

Scroll to **SephiriaOne shortcut**, choose **Set / Change**, release any held
keys, then press an unused keyboard key. **Clear** removes the shortcut;
**Cancel** preserves it. Native navigation/modifier keys and existing game
bindings are rejected. This is a single keyboard key, not a chord or gamepad
binding. Native Escape/cancel can close Settings and cancel capture; the addon
does not add a second Escape handler.

The preference is written immediately to
`%USERPROFILE%/AppData/LocalLow/TEAMHORAY/Sephiria/SephiriaOne/ui.json`
(`Application.persistentDataPath/SephiriaOne/ui.json`). No file is required until
you save a preference. A saved unassigned preference uses this schema:

```json
{ "version": 1, "key": null }
```

A chosen key is stored by its invariant Input System `Key` name, for example
`"F8"` if that key is available in your native bindings. Failed writes preserve
the active shortcut; malformed or unsupported config starts unassigned with a
log warning. The shortcut is excluded from `/one save`, `/one forget` and
gameplay resets. Use Settings to change it rather than editing a live config.

Chat, text fields, foreign menus and binding capture suppress the shortcut.
A press ignored in those contexts does not activate later if the view closes
while the key is still held. Capture also waits for release before accepting a
key, and completing capture does not open the window. Native conflicts are
checked again on shortcut use in case the game was rebound afterward.

The installed title and session scenes both expose the keyboard list as a
`ScrollRect` whose content has `VerticalLayoutGroup` and `ContentSizeFitter`.
An addon-owned block can join this list without resizing native tabs, changing
their arrays, or cloning listeners. Removing the block lets native layout
reflow and leaves other mods' controls intact.

The native `RebindActionUI` is deliberately not used for capture: its completion
handler can clear or swap conflicting game bindings. The addon checks conflicts
read-only and owns its local preference and capture lifetime.

## Structure and performance

- `SettingsPanel` keeps tab forms, drafts and shared command dispatch together.
- `PanelWidgets` creates reusable text, input, button, checkbox, dropdown and
  scrolling controls. The existing Combat tab supplies the damage slider.
- `PanelWindowGeometry` handles canvas-local sizing and clamping;
  `PanelWindowDrag` handles pointer events only on the title area.
- `PanelToggleState` and `PanelCheckbox` separate user intent from silent
  observation updates and preserve enable/disable compatibility differences.
- `SettingsPanelController` resolves a registered screen UI root independently
  of the optional pause launcher and retains the existing cleanup retry protocol.
- `UI/Hotkeys/` owns the local preference, capture/press policy, keyboard adapter
  and native Settings block. It calls the same owned-window `TryToggle` entry.

Hidden panels do not request settings snapshots. Visible observations retain
the 0.25-second cadence; full diagnostics are requested only on Status. Controls
are rebuilt on tab/language changes, not during ordinary refresh. Text readouts
are remeasured only when their contents change. Dragging uses pointer events;
there is no blur, extra render texture, external asset or guest protocol.

The shortcut consumes a cached key's press edge before querying native focus or
bindings. Idle operation does not scan key lists, text-field hierarchies or
native captures. Full key scans occur only while listening; native Options
discovery/retry is bounded to once per second. The native Settings block refreshes
only after a binding/capture/feedback change or localization revision.

## Evidence and remaining live checks

Native inspection used Sephiria `1.0.33` and the assembly fingerprint recorded in
the [investigation](ui-revamp-investigation.md). Serialized `level1` and `level2`
layouts confirmed the keyboard-list insertion point. Native references remain
non-copying, including the installed Input System dependency.

Integrated verification on 2026-10-10:

- Non-deploying Debug and Release addon builds: zero warnings/errors.
- Release portable suite and installed-game contracts passed, including existing
  panel lifetime, dispatcher and localization contracts plus the new widget,
  root, hotkey API, layout and owned-cleanup checks.
- Window fixtures: 10 geometry/checkbox checks and 12 launcher lifetime checks.
- Hotkey fixtures: 34 policy/storage/ownership checks. Localization loader: 50;
  bundled catalog checks: 2,644. Both EN/KO catalogs include the new UI messages.
- Runtime command/session suite: 1,255 checks plus 69 Bat lifecycle checks.
  Existing five-player performance budgets passed with 0 measured bytes per
  unchanged tick in their three gameplay fixture scenarios. Non-Status snapshots
  remained 25,432 bytes/refresh versus 116,558 for full Status in that fixture.

All commands used `-p:DeployMod=false`. The broad portable IL-rewrite fixture
requires `-c Release`; its known Debug-only reward-loop shape failure is unrelated
to this UI change. The hotkey/window pure fixtures run independently of that
fixture. The allocation figures concern existing .NET gameplay/snapshot paths,
not Unity canvas rendering or the complete addon.

Automated fixtures and installed-DLL contracts do not run Unity's rendering or
input event loop. No game launch or deployment is part of this work. After
manual installation, check EN/KO wrapping, dropdown scrolling, dragging and
Center at different resolutions; shortcut capture/cancel/clear and restart
persistence; native rebinding conflicts; chat/IME focus; repeated open/close,
session re-entry and addon unload; and native focus restoration after closing.

See the [user guide](control-panel.md), [module map](module-structure.md) and
[implementation plan](superpowers/plans/2026-10-10-tabbed-settings-ui.md).
