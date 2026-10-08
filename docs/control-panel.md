# Host settings panel

Added in `0.13.0`. In a hosted town or run, open the pause menu and click
**SephiriaOne** in its upper-right corner, or enter **`/one ui`** in chat.
Close with **X** or the game's existing Escape/cancel action. No new hotkey or
binding is installed. The first version uses mouse controls and keyboard entry;
full gamepad navigation is not implemented.

The host uses this panel to change settings for **all current and joining
players**. Guests can use the unmodified game. The panel itself is host-local;
effects use the same native synchronization as the existing commands.

Version `0.34.0` groups Bat and default-off [Collin](collin-starting-artifact.md)
controls under **Costumes** (의상). Collin On/Off/Reset takes effect at the next
costume equip or fresh-run restock for Mole, Farmer Squirrel and Turtle. Each
control has its own compatibility gate, so an unavailable Collin hook does not
disable working Bat controls. EN/KO details explain current intent and timing.

Version `0.33.0` added **Bat**: On changes Wingless Bat's costume HP-steal bonus
from 5 to 1; Off/Reset restores native 5. Other HP-steal sources remain independent.
See [scope, preview limits and verification](wingless-bat.md).

Version `0.32.0` adds **Jars** and wraps the tabs onto two rows. Enter a percentage
or `xN`, then Apply; Reset restores each random location's native spawn chance.
Only future normal random spawn checks change. Hidden-room rewards remain native.
See [Mystic Jar scope and verification](mystic-jar.md).

Version `0.31.0` adds **Combat**: friendly fire On/Off, a 0–300% allied-damage
slider with **Apply damage**, and Reset (off/100%). Dragging is a local draft;
the Apply button uses the same `/one friendlyfire damage` service as chat.
Since `0.37.0`, enabling this also makes companions target other players while
protecting their owner. Off/reset or 0% stops player targeting and immediately
blocks companion hits against players, including attacks already in flight.
See [friendly-fire scope and verification](friendly-fire.md).

Since `0.21.0`, `/one language en` or `/one language ko` selects the panel and
message language. Editable JSON catalogs and config are described in
[localization](localization.md). Language changes refresh open views and clear
unapplied drafts without sending any gameplay command.

## Controls

| Tab | Actions and observations |
| --- | --- |
| Stats | Select one of the ten supported stats with the arrows, enter an amount, then Set, Add or Subtract. Reset selected or all stats. Current per-player values use the stat's displayed units. |
| Fountain | Set, add or subtract whole-number points for everyone; reset the addon adjustment. Shows current capacity and tracked contribution per player. |
| Choices | Select all, item, weapon or miracle. Set/add/subtract **extra** candidates, or reset selected/all categories. Shows current effective native extra-choice stats. |
| Resources | Select dice, inventory slots, talents, fruit-skewer budget or leaves. Set/add/subtract, `xN`, and selected/all resets. Dice/leaves show current balance separately from the next fresh starting allowance. |
| Presets | Save current applied settings, forget the saved copy, or refresh it from disk. Active intent and saved intent are shown separately. |
| Status | Current intent, player values, revisions, synchronization outcomes, native name status and fault details. Scroll for the complete readout. |
| Items | Default-off On/Off and Reset controls for given-item sale restrictions and owner-bound drops, including Fountain sharing. See [scope and native limits](item-restrictions.md); intrinsic costume-curse no-drop properties remain. |
| Combat | Friendly fire On/Off, a 0–300% damage slider with Apply damage, and Reset (off/100%). |
| Jars | Apply a 0–100% chance or native-rate multiplier such as x2. Reset restores native. Does not reroll existing jars, change hidden-room rewards or create new spawn locations. |
| Bat | Default-off reduction of Wingless Bat's own HP-steal bonus from 5 to 1. On/Off, Reset and active status; equipment and buff bonuses remain native. |
| Rabbit | Independent On/Off controls for Wing-Eared Rabbit infinite HP potions, nearby potion healing, MP cost, Survival rank-5 suppression and random non-HP/MP potions on level-up. Enter 0..10000 MP and press Set & on to choose a fee; Off remembers it. Reset restores 10 MP and disables all five options. Shows active state and separate drink/level-up compatibility. |
| Merchant | Select Wandering Merchant, Papyrus or Taz. Each has spawn On/Off, a separate Guarantee On/Off (default on), chance 0..100%, first main dungeon stage 1..1000, run limit 0..1000 (0 = unlimited), and Reset selected. Enter a number, then choose its action. Guarantee off leaves chance rolls active and releases the reserved cap slot. Multiple types can share a floor. Added merchant base HP uses main-stage multipliers 1/2/4/5/7/8 for stages 1–6 (later stages stay ×8; maps within a stage share the factor), retaining native HP bonuses and no crime penalty. |

Actions only run when clicked. Typing, switching tabs and refreshing observations
never send a settings command. Successful actions clear the amount field. Changing
the stat/category or session/run clears stale drafts. Per-player rows are
observations, not controls for targeting individual players.

For stats, `Add 10` then `Add 5` means each character's native stat plus 15.
After `Set 100`, `Add 10` switches to native plus 10. The existing synchronization
service maintains relative displayed offsets after native stat changes.
Use a decimal point for fractional units. Catalog limits and precision are shown
beside the selected stat; the shared parser and service validate every action.

Since `0.14.0`, enter `x3` and click **Set** on Stats or Fountain to maintain
three times each character's native value. `x1` restores native. Fractional
factors such as `x1.5` require exactly representable results; Add/Subtract after
a factor starts a fresh native-relative offset. See [multipliers](multiplier-command.md).

Choices retain their existing 0..20 addon contribution limit and native
multiplier behavior. Already-generated offers keep the game's cache behavior.
Reset removes tracked addon changes and preserves native bonuses. There is no
cross-family Apply All or Reset All operation.

Save stores applied intent, excluding any text still in the amount field.
Forget removes only the saved copy. Resets affect the current session; save again
to replace a saved preset, or forget it to stop automatic loading in future hosted
sessions. Automatic-load rules are unchanged; presets containing multipliers
use v2, resource presets use v3, original rabbit options use v4, and rabbit MP-cost
or Survival-suppression options use v5. Custom Rabbit MP amounts use v6, including
amounts retained with charging off. Merchant settings use v7, including a custom
chance retained while off. Typed merchant conditions use v8; nondefault guarantee
options use v9. Reset selected restores guarantee on and spawns off. Preset changes
do not replenish consumed run history. Older setting types retain v1 compatibility.
See [resource semantics and safe reductions](resource-command.md). Since 0.15.0,
`/one` replaces the old generic `/mod` token, which is no longer intercepted.

The name gradient remains automatic, fixed at `#408af1` to `#a8d7fa` and read-only.
The panel does not add color settings or style platform lobby names. See the
[compatibility inventory](presentation-compatibility.md).

## Readiness and lifetime

The panel joins the native UI control stack. Opening it above the pause menu
preserves that menu underneath; opening it from chat does not force the game to
pause. Multiplayer continues running. Native gameplay-input gating and cancel
handling are reused, with no extra EventSystem or Escape polling.

The controller closes the panel when host authority or the connected player is
lost, and disposes/rebinds it when the native UI manager/pause panel changes.
After an ordinary run/session change, any surviving panel clears the old draft
and observes the new scope. An action rechecks that scope at click time.

Ordinary edits are disabled while a participant is initializing or a write is
faulted. Matching whole-family recovery resets remain available, with final
validation in the existing service. Save follows preset-service readiness;
forgetting a saved file does not require a ready player. If UI creation or refresh
fails, the existing chat commands remain available and `Player.log` records it.

Cleanup checks actual native stack membership. If a callback throws before
removal, the hidden panel is retained for retry; if removal already completed,
it is not sent twice. Unload can leave a cleanup-only retry object until that
native entry is released. Persistent failures in other native/addon callbacks
can still require leaving the session; they are reported rather than bypassed.

Live values refresh every 0.25 seconds while open. They are host-side observations,
not acknowledgments of guest rendering. Saved-file reads are cached until open,
explicit refresh/status inspection, save/forget, or controller reload. Refresh
the saved copy after editing the file externally.

## Shared implementation

- `Controls/SettingsActions.cs` dispatches both chat and panel requests through
  the existing parsers and authoritative feature/preset services.
- `SessionSettings.ReadSnapshot` returns copied read-only values, permissions,
  status and preset summaries. It never reconciles state or enrolls a newcomer.
- `UI/` owns fresh uGUI/TMP controls, a `UIBase` panel, native bindings and drafts.
  It reuses the installed font/root and creates no gameplay patches or assets.

Native setup was inspected against Sephiria `1.0.33`; see the
[investigation](control-panel-investigation.md) for the assembly fingerprint and
[implementation plan](control-panel-implementation.md) for verification evidence.
Game assembly references remain non-copying; the package is still the addon DLL
and metadata, with its existing embedded Harmony dependency.

## Live verification still required

Automated checks do not execute Unity's event loop or a real multiplayer session.
No deployment was performed for this change. After manual installation, verify:

1. Open through both entry points, close with X/Escape, and reopen repeatedly.
   Confirm native menu focus is restored and held movement/attack input does not
   leak through the open panel. Check scrolling, numeric typing and screen scaling.
2. Compare panel and chat actions for every family, including rejection messages,
   decimal stats, resets, distinct character baselines and existing cached offers.
3. With an unmodified guest, apply settings, join/reconnect, change native
   loadout/stats, finish/restart runs, and verify native effects and Fountain item
   carryover. Confirm multiplayer keeps running while the panel is open.
4. Type a draft, cross a run/session boundary, and confirm it is cleared. Verify
   ordinary refresh preserves text and never applies it. Disconnect and reload
   the addon; check no duplicate menu button, stuck input or leaked UI stack.
5. Save, alter active settings, inspect both summaries, forget, and restart the
   game. Confirm only the explicitly saved policy persists and drafts do not.

See also [commands and presets](session-preset.md),
[relative-stat semantics](relative-stat-consistency.md) and
[shared synchronization and recovery](synchronization-guide.md).
