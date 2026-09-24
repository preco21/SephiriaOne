# Settings control-panel investigation

Investigated 2026-09-24 against addon `0.12.2` and installed Sephiria `1.0.33`.
This is the original feasibility report. The recommended approach was subsequently
implemented in `0.13.0`; see the [panel guide](control-panel.md) and
[implementation record](control-panel-implementation.md). The findings below
describe the investigation stage, which made no runtime changes or deployment.

## Recommendation

Build an addon-owned uGUI/TextMeshPro panel integrated with the game's `UIBase`
control stack. Only the host needs the panel; it is another input surface for
the existing native-synchronized effects available to unmodified guests.
Keep the chat commands as an alternative interface.

| Approach | Assessment |
| --- | --- |
| Runtime uGUI/TMP panel using native UIBase | Recommended. Fits the installed game's UI, focus, input and lifecycle model; can be built in C# without an asset bundle. Needs a small audited adapter to native UI APIs. |
| Standalone IMGUI debug window | Possible prototype, but would require separate focus, input blocking, scaling and navigation integration. Poor fit for the intended permanent control panel. |
| Additional tab inside native Options | Possible but more coupled to the game's serialized tab/layout and options lifecycle. Prefer a separate panel reached by a menu button. |

The installed game already uses uGUI, TMP and the new Unity Input System.
[Unity's UI comparison](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html)
also describes uGUI as a runtime UI option; the recommendation here primarily
follows the inspected game's existing implementation.

## Confirmed native integration points

| Inspected code | Finding and implication |
| --- | --- |
| `HorayModAPI`, `HorayModBase` | No settings-panel/menu registration interface in the installed types. Existing lifecycle hooks can own a controller; the panel needs its own native UI adapter. |
| `UIBase.SetRoot`, `Open`, `Close` | A panel can attach to an existing `UIRoot`, enter/leave the control stack, and receive native focus behavior. Build it inactive and assign root/control flags before opening. |
| `UIBase.Enable`, `Disable` | Native `CanvasGroup` interaction/raycast gating and default selectable support. Use the existing EventSystem instead of creating another one. |
| `UIRoot.AddControl`, `RemoveControl` | Public methods invoke the callbacks already subscribed by UIManager. A dynamic panel does not need to modify private lookup dictionaries to participate in the control stack. |
| `UIManager.Awake`, `ConnectPlayerObject`, `DisconnectPlayerObject` | UI lookup/connection lists are collected from initial root children. A panel created later is not automatically in those lists; its controller must hold a direct reference and explicitly track UI-manager/player replacement. |
| `UIInputModule.Update` | Native cancel closes controls through `CloseFromEsc`; an empty control stack permits the pause menu. Avoid a second Escape handler that closes two panels in one frame. |
| `PlayerInputController` | Movement, interaction, attacks and skill callbacks consult the native control stack. Use it instead of merely drawing a mouse-blocking overlay. Held-input transitions still need live verification. |
| `ControlsChangeHandler`, `UI_HorayButton` | Existing default selection and directional navigation can support gamepad work. Numeric entry, scrolling and changing control schemes still require explicit design/testing. |
| `UI_PausePanel`, `GameTimeManager.Pause` | Native pause is skipped in multiplayer. Do not claim the panel pauses the session or force timeScale to zero. Opening over the pause menu should preserve its lifetime. |
| `UI_OptionsPanel.OnClosed` | Closing the native options panel saves input-binding overrides and game options. Do not clone its behavior for addon settings. |

Native findings refer to `Assembly-CSharp.dll` SHA-256:

```text
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
```

Decompiled research remains outside the repository. The official API site
`https://teamhoray.com/mod-api` was inaccessible through the web tool during this
investigation; the installed assembly is the evidence for the API findings.

## Proposed controls and exact semantics

Display a persistent scope label: **All current and joining players**. Player
rows are read-only observations, not individual targeting controls. Changing
target scope would be a separate feature.

| Area | Controls | Existing behavior to preserve |
| --- | --- | --- |
| Fountain | Whole-number amount; Set, Add, Subtract; Reset | Uses `FountainPoints.TryExecute`; capacity and carryover validation remain authoritative. Show each player's current points and addon contribution. |
| Choices | Item, weapon, miracle or all; extra amount; Set, Add, Subtract; category/all reset | Uses `ChoicePoints.TryExecute`. Values mean extra candidates, not total candidates. Addon contribution is 0..20 with native resulting-value checks; existing offers remain cached. |
| Stats | Stat selector or rows generated from `StatCatalog.All`; amount; Set, Add, Subtract; stat/all reset | Uses `CharacterStats.TryExecute`. Units, precision and limits come from the catalog/shared validation. Relative deltas accumulate from each player's own native stats; adding after set switches to relative mode. |
| Preset | Save current settings; Forget saved preset; active/saved summaries | Save persists applied intent, not field drafts. Forget removes only the saved copy. Loading remains automatic for the next hosted session; there is no existing manual Load action. |
| Status | Ready-player count, per-player effective values, retained intent and waiting/suspended/faulted reasons | Read-only, matching `/one status`. Do not label local native readback as confirmed guest rendering. Show family recovery resets when partial-write recovery requires them. |
| Name gradient | Current fixed colors and native publication status | Gradient is currently automatic and hard-coded, with no name command, toggle or saved color setting. A first panel can report it without implying it is configurable. |

Supported stats: luck, defense, attack speed, critical chance, critical damage,
evasion rating, cooldown recovery, MP regeneration, negotiation and true damage.
Show friendly labels with their actual units: evasion is not dodge percentage,
attack speed uses 100 for normal, and critical damage is a bonus percentage.

Use explicit action buttons, not live changes on every slider step or keystroke.
Clear a successfully applied delta draft so a later refresh does not suggest it
is still pending. Reset means removing the tracked addon adjustment, preserving
native changes; it does not mean assigning zero or a fixed character default.

Start with one operation at a time using the existing family services. Do not
advertise an atomic cross-family Apply All/Reset All by sequentially calling three
commands: later failure could leave earlier families applied. A true combined
operation would need a separate all-family plan and shared write batch.

## Shared architecture needed

```text
Chat parser ---------+
                     +--> shared validated action service
Panel draft/actions -+          |
                                v
              existing Fountain / Stats / Choices / Preset services
                                |
                   SessionSettings + native writes/replication

Session state --> read-only typed snapshot --> panel and chat status formatting
```

1. Extract shared action dispatch/result handling from `ModChatCommands`, leaving
   its chat binding and chat suppression in place. Reuse the existing command
   parsers or shared validated factories; unchecked internal command constructors
   are not a replacement for input validation. Keep host/readiness checks in the
   execution services even when the UI disables its buttons.
2. Expose an immutable settings snapshot: session identity/revision, retained
   policy, per-player values, feature availability, recovery state, name status
   and saved-preset summary. Do not parse human-readable status strings or the
   serialized preset text to populate controls.
3. Share that snapshot with `/one status`. Reading must not call reconciliation,
   enroll a newcomer, restore a cap, or otherwise mutate state. Existing status
   also reads the preset file; cache/refresh that portion on opening or explicit
   inspection/save/forget, not every rendered frame.
4. Keep UI drafts separate from observed state. Refresh observations while open
   and after actions, preserving focused/dirty input. External native-menu,
   equipment, preset and chat changes update the displayed values without sending
   commands. Never silently apply a draft from an old session to a new one.
5. Keep the existing shared synchronization core, policies, journals and native
   transport. No custom peer protocol or resurrection of the removed foreign-UI
   presentation registry is necessary; this view only renders addon-owned state.

Suggested modules: `Controls/` for shared actions/snapshots, `UI/` for panel
construction and interaction, and a narrow native UI adapter. Feature arithmetic
stays in its existing feature folders; session policy stays under `Session/`.
Actual names can follow implementation constraints.

## Entry point, lifecycle and packaging

- Propose a **SephiriaOne** button in the in-session pause menu plus `/one ui`
  as a fallback entry point. The pause-menu prefab's layout and navigation have
  not been inspected yet, so exact button placement remains implementation work.
  An optional hotkey needs a conflict check against native bindings.
- Use a fresh `UIBase` subclass and addon-owned controls, borrowing native font
  and visual assets at runtime where suitable. Do not clone a whole native panel
  with gameplay callbacks or copy serialized button listeners.
- Reuse a verified live root, canvas and EventSystem. Additional references such
  as `UnityEngine.UIModule` and `Unity.InputSystem` may be needed; use the game's
  installed assemblies with `Private=false`. No Unity Editor or extra runtime
  package is required for the proposed code-built approach.
- Close/unbind on lost host authority, UI-manager replacement, session exit and
  unload. Remove only owned buttons/listeners/objects; balance every native
  AddControl/RemoveControl and UI counter exactly once. Reopenings must not add
  duplicate listeners or apply a delta twice.
- Preserve unload ordering: if native contribution cleanup fails, keep command
  and recovery controls available as the current entry point does. Dispose the
  panel only after that cleanup succeeds or its native UI/session is gone.
- Disable mutation during readiness/transition/fault states with the reason
  visible. A per-frame UI observation cannot replace the services' final checks
  or the existing critical native read guards.
- Current recommendation, pending input preference: mouse/keyboard first.
  Preserve native focus/cancel integration so gamepad navigation can be added
  without rewriting settings execution. No gamepad support is claimed yet.

## Name configuration would be separate work

Enabling/disabling native name publication can reuse its existing plain-name
restoration path, but needs an explicit setting and ownership/lifecycle tests.
Editable gradient endpoints additionally require changing `NameGradient` and
`NetworkNameState`, invalidating pending requests, recognizing the previously
published style during replacement/restoration, and defining persistence.
The current formatter recognizes its fixed canonical gradient and legacy blue;
simply replacing the RGB constants with UI values would not cover old markup.

Keep any future name preference separate from the all-player gameplay policy:
it styles only the host's owned name. It still cannot style Steam/EOS lobby
platform names on unmodified guests. Do not bring back those removed features.

## Implementation checkpoints and evidence still needed

1. Add shared action/snapshot interfaces and parity tests for chat/panel requests;
   preserve the current 860 regression checks. Cover invalid units, readiness,
   authority loss, preserved relative semantics and failed-write recovery.
2. Build a minimal panel with open/close, one action and status. Verify native
   focus, no gameplay input leakage, Escape, pause-menu nesting and teardown.
3. Add the remaining controls, active/saved summaries and read-only player rows.
   Verify refresh while editing, repeated clicks, stale drafts, joins/restarts,
   native menu changes, different player baselines and preset operations.
4. Inspect native signatures, build with `-p:DeployMod=false`, and then perform
   live host/unmodified-guest checks when testing is authorized. Native stock
   panels/offers retain their own cache behavior; this panel does not refresh them.

No panel prototype was compiled or run during this investigation. Pixel layout,
font/scaling, input behavior, controller usability and live multiplayer rendering
remain unverified. This report does not change version `0.12.2`.
