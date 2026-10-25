# Tabbed settings window and local hotkey

Introduced in `0.39.0`, following the user's approval of option 2 from the
[UI comparison](ui-revamp-investigation.md). The window uses the installed
Unity uGUI/TextMeshPro components and the existing settings services.

## Grouped navigation and rounded controls (`0.95.0`, 2026-10-25)

The current window is **940×560** with a scrollable left navigation rail and a
larger content area. This supersedes the historical horizontal-tab layouts below.
The requested Korean hierarchy is:

| Section | Destinations |
| --- | --- |
| 능력치 및 자원 | 스테이터스, 소원 분수, 보상 선택지, 자원, 아이템 |
| 코스튬 기능 | 날개귀 토끼 힐러 옵션, 기타 코스튬 옵션 |
| 재미 | 황금 고블린 상인, 이벤트 확률 조정 |
| PvP | PvP 전투, 데스매치, 토너먼트, 술래잡기, 좀비 서바이벌, PvP 밸런싱 (기본), PvP 밸런싱 (지속 타격) |
| 성장 | XP 획득 제한 |
| 최적화 | 투사체 최적화 |
| 시스템 | 로그, 설정 프리셋, 업데이트 |

Each section has a small mesh-drawn icon (bars, rabbit, die, crossed swords,
sprout, lightning, gear). System uses quieter colors. The rail supports wheel,
dragged scrollbar and up/down buttons; keyboard selection and page shortcuts
reveal offscreen destinations. All 21 stable page IDs, snapshot scopes, action
routing and draft lifetimes are preserved. Rabbit already contained only its
own healing/potion options; other costumes remain separate.

Main and internal surfaces, buttons, input fields, dropdowns, slider handles and
filled-square checkboxes have softened exponent-three corners and subtle borders.
Single-row labels align vertically with inputs. Long lists have more height.
Every scroll region has arrow buttons, including stat/source/Slate dropdowns,
readouts, player selection and both balancing columns. The shorter two-Slate
popup retains correctly anchored arrows and viewport when TMP resizes it.

Routine ready/host/player-count header labels and idle match descriptions were
removed. Compatibility failures, active match status, action results and global
installation/restart warnings remain visible. English and Korean navigation
labels are bundled; existing custom translation files are preserved.

### Shared implementation and performance

- `PanelNavigation` holds group order and existing page IDs; page builders remain
  responsible only for their own controls.
- `PanelSurface : Image` draws a single fill/border mesh with a cached corner
  curve. It retains native UI material, tint, masks and batching; no custom
  texture, shader or per-frame poll. A normal bordered surface has 85 vertices.
- `PanelSectionIcon` draws font-independent icons; no Unicode availability risk.
- `PanelWidgets.AddScrollbar` owns the common track/arrows. `PanelScrollArrow`
  uses serialized references and balanced enable/disable listeners, so Unity's
  TMP template cloning binds to the displayed popup rather than its template.
- `PanelScrollMath` provides bounded pixel steps and selection reveal;
  `PanelNavigationFocus` runs on selection only. Existing cached readouts and
  pooled participant rows remain unchanged.

Validation covers all destinations/order, scroll limits/focus, 25,320 shape
checks (including zero allocation across 10,000 geometry samples), localization,
installed UI/lifecycle/command contracts, and a non-deploying Release build.
An offline layout inspection is not proof of native font rasterization or Unity
interaction. Live EN/KO wrapping, hover/focus, short dropdowns, scroll arrows,
window scaling and dragging still need an in-game smoke test. No gameplay or
network policy changed, and no build was deployed.

## Window

Open with `/one ui`, the **SephiriaOne** pause-menu button, or your configured
shortcut. The window is separate from the pause menu. Drag its title bar to
move it. The title bar has a **Close** button; the Center button was removed
on 2026-10-10. The window clamps to the current canvas and
scales down for smaller screens. Its position lasts for the current UI instance.

Version `0.82.0` uses a **760×560** window and three rows of seven wider tab
buttons. **PvP Balance / PvP 밸런스** is a dedicated tab beside Combat, with
general controls left and a taller debuff/Dark Cloud list right. Combat shows
its own controls and supplies together. See the [current layout](control-panel.md#dedicated-pvp-balance-tab-0820-2026-10-21).
The page requests base settings only, without participant detail or new polling.

Version `0.83.0` adds **Rapid Hits / 연속 타격** beside PvP Balance, keeping the
same window and three-row geometry. It separates hit-history controls from
stored damage allowance and continuous refill, with cached applied-value
examples. It also requests no participant detail. See the
[rapid controls](control-panel.md#rapid-hit-controls-0830-2026-10-22).

Historically, the 2026-10-10 compact pass reduced the window from 800×480 to 760×420. The
14 tabs remain in two rows; forms use a 1.25 scale instead of 4/3. The title bar
contains a short scope/update notice. Action feedback starts blank and retains
autosizing for longer validation results. Add, Subtract and Close use `+`, `−`
and `×` consistently through the shared button factory; callbacks and semantic
object names are unchanged. Set, Reset, Save and recovery actions retain text.
Repeated host/persistence instructions were removed and EN/KO feature notes
shortened while retaining timing, limits, exceptions and compatibility warnings.
This pass passed Debug/Release builds, existing window fixtures and the full
portable/installed-game contract suite, including 3,066 catalog checks. No new
input or gameplay hooks were added. Actual EN/KO rendering remains a manual check.

Historically, tabs were **Stats, Fountain, Choices, Resources, Presets, Status, Rabbit,
Merchant, Items, Combat, PvP Balance, Spawns, Costumes, Updates, Deathmatch,
Tournament, Play Tag, Zombie and Progression**. Stats
also has a scrollable selector for supported visible stats. Boolean
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
the 0.25-second cadence. Each tab requests only its displayed snapshot sections;
readiness, permissions and feature settings remain current on every capture.
Full diagnostics are requested on Status; unknown future tabs default to a full
capture until assigned their sections. See the [measured follow-up](performance-review.md).
Controls are rebuilt on tab/language changes, not during ordinary refresh. Text readouts
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
manual installation, check EN/KO wrapping, dropdown scrolling and dragging
at different resolutions; shortcut capture/cancel/clear and restart
persistence; native rebinding conflicts; chat/IME focus; repeated open/close,
session re-entry and addon unload; and native focus restoration after closing.

See the [user guide](control-panel.md), [module map](module-structure.md) and
[implementation plan](superpowers/plans/2026-10-10-tabbed-settings-ui.md).
