# Settings UI revamp investigation

Investigated 2026-10-09 (Asia/Seoul), repository `78ab740`, addon `0.38.0`.

**2026-10-10 follow-up:** the user approved option 2 with tabs and native Settings
hotkey configuration. Version `0.39.0` implements that direction; see the
[current implementation and verification limits](tabbed-settings-ui.md).
The comparison and proposal below record the earlier investigation.
Scope: compare native Settings integration, a separate translucent/draggable
window, and a refresh of the existing panel before implementation. Text, inputs,
buttons, sliders and checkbox toggles are required. This record contains a
recommendation, not an approved implementation spec or a live UI test.

## Recommendation and comparison

Recommend **option 2**, evolving the existing uGUI/TextMeshPro panel into a
separate window while retaining native `UIBase` control-stack integration and
the current shared settings services. Reuse native fonts and suitable visual
assets through a narrow adapter; keep control instances and their listeners
addon-owned. Option 3 is the smaller first step if scope must be limited.

| Approach | Feasibility / effort | Limitations and maintainability | User experience |
| --- | --- | --- | --- |
| 1. Embed a SephiriaOne tab/page in native Settings | Feasible from the inspected public tab arrays and content classes; medium-to-high integration effort. All required widgets can be added. There is no HorayModAPI settings-page registration method. | Coupled to native Options layout, tab arrays, navigation, initialization and save lifecycle. Native option binders write local game settings, which must not store host session intent. Other mods may also append tabs. Main-menu access does not imply a ready hosted session. | Most familiar look and existing menu discovery. Still buried in Settings, constrained by its available space, and can blur the distinction between local options and all-player changes. Native gamepad support does not automatically wire new rows. |
| 2. Separate translucent, draggable window using native uGUI/TMP | Feasible; medium effort because action dispatch, snapshots, localization and stack cleanup already exist. Dragging and checkboxes are supported by installed Unity APIs. | Addon owns layout, drag bounds, focus, entry-point and lifecycle behavior. Requires decoupling the current root/font lookup from the pause panel. Smaller native dependency surface than changing Options tabs. | Best fit for the number of features: sidebar/categories, readable scrolling forms, visible game scene, movable window and direct command access. Requires deliberate input blocking while editing; transparency does not make editing safe during combat. |
| 3. Keep current panel and visually refresh it | Highest immediate feasibility and lowest effort/risk. Existing pause button and `/one ui` remain. | Retains the current controller lifecycle. A color-only change leaves fixed geometry and growing page-switch logic; reusable rows/layout are still useful. Substantial movement/access changes eventually become option 2. | Faster improvement to spacing, typography, selected-category state, checkboxes and status. Less change to user habits, but a fixed modal panel remains less flexible. |

All three preserve host-only gameplay installation: they are local controls for
the existing services, and guests receive existing native effects. None requires
a new network protocol or guest UI assets. These are engineering assessments,
not measured implementation schedules or performance benchmarks.

## What the current panel already provides

The current UI is already custom, code-built uGUI/TMP; it is not a cloned native
Options panel. It opens through the ESC-menu button or `/one ui`. The command
does not itself open/pause through ESC, although its controller still requires
`UI_PausePanel` to supply a root and font.

Source findings:

- [SettingsPanel](../SephiriaOne/UI/SettingsPanel.cs) has a fixed 600×356 logical
  window, two rows of six page buttons, a separate Updates button and a
  600×162 page area. Most labels use 9–12 logical-unit text. This describes code
  geometry, not measured on-screen pixels at every resolution.
- It draws a full-root background at alpha 0.9 and an opaque window. It has no
  drag controller, checkbox factory, category search or flexible settings-row
  layout. Arrow cycling becomes cumbersome for the 27-stat catalog.
- [PanelWidgets](../SephiriaOne/UI/PanelWidgets.cs) creates text, input, button
  and readout scrolling. [Combat](../SephiriaOne/UI/SettingsPanelCombat.cs) builds
  its slider separately. Buttons explicitly use `Navigation.Mode.None`; full
  keyboard/gamepad traversal is not implemented.
- [SettingsPanelController](../SephiriaOne/UI/SettingsPanelController.cs) refreshes
  the visible panel every 0.25 seconds and handles rebind/disposal. Existing
  [PanelControlLifetime](../SephiriaOne/UI/PanelControlLifetime.cs) retains cleanup
  when native callbacks fail; it must survive a visual rewrite.
- Actions use [SettingsActions](../SephiriaOne/Controls/SettingsActions.cs), and
  views read immutable [snapshots](../SephiriaOne/Controls/SettingsSnapshot.cs).
  [PanelDraft](../SephiriaOne/UI/PanelDraft.cs) guards session/run identity. These
  are reusable foundations, not reasons to change synchronization.

## Native Settings and window integration evidence

Fresh selected-type decompilation used the installed `Assembly-CSharp.dll`:

```text
SHA-256 C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
```

Temporary exports are under `%TEMP%/sephiria-ui-revamp-research/`; they are not
repository dependencies and are not committed. No Unity scene was launched or
rendered for this investigation, and exact native Settings asset layout has not
been re-audited here.

| Native symbol | Observed behavior / implication |
| --- | --- |
| `HorayModAPI` | No general settings registration API. A native tab needs a maintained game adapter. |
| `UI_OptionsPanel.OnOpened` | Selects tab 0 and assigns its default selection. Appended-tab selection/focus needs explicit handling. |
| `UI_OptionsPanel.OnClosed` | Saves action binding overrides and calls `OptionsBinding.Save`. Do not clone this controller or substitute its storage for addon presets. |
| `UI_Tab` / `UI_TabContent` | Public button/content arrays, `SelectTab`, content activation, `selectionOnOpened` and selection notification make extension plausible. Arrays, indices and navigation must remain consistent with other extensions. |
| `UI_OptionBox_Common_Integer` | On enable reads a named local Options/DeviceOptions value, then writes that store on changes. Its binding is not a host session setting abstraction. |
| `UI_VolumeSlider` | Subscribes on enable and writes both sound state and local options. Reusing its whole component for a gameplay slider would be incorrect. |
| `UI_MuteBackgroundAudioToggle` | Uses a standard `Toggle`, but its binder writes the native audio option. Fresh toggles can reuse visuals without this behavior. |
| `UIBase`, `UIRoot`, `UIManager` | Support owned panels with native interaction/cancel accounting. Manager registers root callbacks at initialization and publicly exposes `uiRoots`; dynamically creating an unrelated root does not automatically wire the native stack. |
| `UIInputModule.Update` | Native cancel closes controls. Opening pause depends on an empty control stack. Avoid a second Escape handler and preserve stacking order. |
| `UI_HorayButton` / `ControlsChangeHandler` | Offer focus and directional-navigation facilities, but new controls still require explicit navigation and testing. |
| `UI_Drag` | Draws a world-position selection rectangle; it is not a draggable-window component. Use uGUI pointer-drag handlers on the title bar instead. |

Option 1 should append only owned tab/content objects, retain references and
remove only those objects on teardown. Restoring a stale copy of the entire tab
array could erase another mod's later additions. Copying native control objects
can retain persistent serialized callbacks and native `OnEnable` subscriptions;
fresh widgets with read-only visual references are easier to audit.

For option 2, resolve a suitable existing, manager-registered screen-space root
and font independently of the pause panel. The optional ESC entry can remain an
adapter, not a prerequisite for creating/opening the window. Validate canvas,
raycaster, sorting and root visibility at runtime; the correct root is not proven
by choosing an arbitrary enum value or creating a second EventSystem.

## Required control support

Every approach can expose the same reusable control set; it is not restricted to
whatever rows happen to exist in native Options.

| Element | Proposed primitive | Binding behavior |
| --- | --- | --- |
| Labels, help and status | `TextMeshProUGUI` | EN/KO localization, wrapped descriptions and readable contrast; dynamic values remain observations. |
| Text/numeric input | `TMP_InputField` | Preserve focused drafts; validate via existing services on explicit action. Allow decimal/xN syntax only for families that support it. |
| Button | `Button` or narrowly configured native button | One explicit command/action; distinguish Apply, Reset, Save and Update. |
| Slider | `Slider` with numeric readout/input | Edit a draft while dragging; commit via Apply. Avoid issuing a multiplayer mutation for every pointer movement. |
| Toggle / checkbox | `Toggle` with checked/unchecked graphic | A deliberate click can send one existing on/off command. Refresh with `SetIsOnWithoutNotify`; restore observed state and show the reason if the command fails. |
| Selection and long forms | Scroll list/dropdown, `ScrollRect`, layout rows | Choose stat/resource/merchant without repeated arrow cycling; keep selection stable during refresh. |

Installed `UnityEngine.UI.dll` directly confirms `Toggle.SetIsOnWithoutNotify`,
`Slider.SetValueWithoutNotify` and `IDragHandler`. The current addon already uses
TMP input and no-notify slider refresh. Unity documents
[checkbox refresh without callbacks](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Toggle.html)
and [pointer dragging](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.EventSystems.IDragHandler.html).
These mechanisms prevent observation refresh from masquerading as user edits.

For transparency, lower the background Image's alpha while keeping labels and
controls opaque. A parent alpha affects children too. Treat opacity and input
capture as separate concerns: `CanvasGroup.blocksRaycasts` affects UI raycasts,
not the game's full movement/attack action gating. Keep the existing native
control-stack behavior instead of relying on the translucent rectangle.

## Proposed option-2 structure and experience

This is a concrete direction for a later approved implementation:

1. **Window shell:** a translucent dark panel, opaque readable controls, title-bar
   drag handle, close button, clear all-player scope, active category and inline
   feedback. Use a category sidebar and scrolling settings forms to replace
   crowded tab rows. Put detailed player/sync diagnostics in an expandable or
   dedicated view instead of competing with every setting.
2. **Widget layer:** one style/spacing/font system and factories for the elements
   above. Use shared setting-row metadata where a feature is repetitive, while
   leaving existing planners and validators authoritative. Avoid a reflection
   framework that assumes all settings have the same semantics.
3. **Bindings:** adapt typed snapshots to visible values. Do not parse localized
   status strings. Extend a snapshot with typed values if a new view needs data
   currently available only as formatted text. Keep pending input separate from
   applied state and the explicitly saved preset. No cross-family Apply All is
   implied by a common UI; that would need a real combined transaction.
4. **Native adapter:** retain `UIBase` and the tested lifetime/cleanup protocol,
   live manager/player checks, host authority, recovery actions and safe teardown.
   Separate the root/font resolver from optional menu/HUD launchers.
5. **Window placement:** drag only the title bar; convert pointer coordinates into
   the actual canvas space and clamp the window/title bar within visible bounds.
   Re-clamp after resolution/UI-scale changes and offer Center window. Remember
   position at least while the current UI lives; any cross-launch preference
   belongs in local UI config, not gameplay presets.

Start with `/one ui` for direct access, retaining the ESC button as an optional
shortcut into the same window. A small in-game launcher could remove command
typing without a key binding; its placement needs a HUD overlap check. **No new
hotkey is required or selected.** Ask the user before adding one, including its
binding/conflict behavior.

Recommended initial input policy is to block the local player's gameplay controls
while this window is open, as the current `UIBase` panel does. Opening directly
does not pause simulation, and multiplayer keeps running. A visually separate,
movable panel need not be a nonmodal combat overlay. Allowing movement/attacks
outside the panel while it is open would require a separate focused-input design,
especially for text entry, scroll, held actions and native cancel.

Korean is the default, with English support and existing native Korean-font
resolution. Prioritize wrapped labels, adequate row height, visible focus and
status indicators that do not depend only on color. Add explicit keyboard/tab
navigation; full gamepad entry/scrolling must be tested before claiming support.
Checkboxes apply deliberate clicks; numeric text/sliders remain drafts until the
existing action succeeds. A periodic refresh must not overwrite focused input.

## Maintenance, performance and verification

The revamp should remain a presentation change. Keep host-only actions, session/run
draft invalidation, exact per-feature recovery rules, presets and unmodified-guest
effects. Local update/language actions remain separate from gameplay readiness.
New checkbox logic must permit safe Off/reset actions when enabling is blocked
by compatibility checks, rather than disabling both directions indiscriminately.

Reuse the existing 0.25-second visible-state cadence as a starting point. Update
only changed labels/rows; request full diagnostics only on the diagnostics view.
Build controls on open/category changes rather than every frame. Avoid layout
rebuilds while values are unchanged, global scene scans and repeated file reads.
Pointer dragging is event-driven. Simple alpha needs no blur shader, render
texture, external UI library or asset bundle. A transparent window still incurs
UI rendering work; no FPS improvement is claimed without measurement.

Before shipping, verify:

- Shared action parity, no commands during refresh, rejected toggles reverting,
  dirty numeric/slider drafts, validation feedback and safe Off/reset gating.
- Open/close through each entry, nested native panels, held movement/fire keys,
  text focus/IME, Escape closing once, no click-through, and exact stack cleanup
  after exceptions, authority loss, disconnect and manager replacement.
- Run/preset/costume changes and guest reconnect while the view is open; old drafts
  must not apply to a different run/session. No new synchronization path is needed.
- Drag bounds, Center window, resizing, scaled canvases, narrow/wide resolutions,
  long EN/KO strings, scrolling and explicit navigation/controller behavior.
- Existing allocation budgets, non-deploying Debug/Release builds, installed-game
  contracts and live solo/host with stock guest testing. Code inspection cannot
  prove actual focus, readability, input timing or visual placement.

This investigation changes documentation only. It does not build, deploy, alter
native settings, change gameplay, select a hotkey or implement the new UI.

Related material: [current panel](control-panel.md), [original investigation](control-panel-investigation.md),
[shared synchronization](synchronization-guide.md), [performance reviews](performance-review.md),
[localization](localization.md), [host-only scope](presentation-compatibility.md).
