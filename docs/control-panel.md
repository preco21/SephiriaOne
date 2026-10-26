# Host settings panel

## Grouped left navigation (`0.95.0`, 2026-10-25)

The current [window layout](tabbed-settings-ui.md#grouped-navigation-and-rounded-controls-0950-2026-10-25)
uses seven sections, a vertical left rail and rounded native controls. The
21 existing destinations and command/snapshot contracts remain stable. This
supersedes the older horizontal layouts and dimensions described below.

## Weak-hit assistance (`0.90.0`, 2026-10-23)

The left **PvP Balance** pane now scrolls to expose the lower-bound toggle,
opponent-HP reference and bonus refill controls without compressing the existing
guard/amplification rows or the independent right DoT list. EN/KO labels, Apply,
per-number reset and family reset use the shared host command path. Defaults:
off, 4% reference, 8% HP/s refill. See [model and limits](pvp-lower-bound.md).

## Teleport all button (`0.89.4`, 2026-10-23)

Combat's left column includes **Teleport all players / 모든 플레이어 소환** below
the potion controls. Small spacing adjustments keep all controls inside the
existing 250-unit page. The action uses the shared `/one teleportall` command,
session guard and feedback area; it is independent of PvP participation and
availability. See [eligibility and native transport](teleport-all.md).

## Dedicated Projectiles tab (`0.88.1`, 2026-10-22)

**Projectiles / 투사체**, beside Spawns, groups the limiter enable toggle,
total per-player cap, default per-source cap, source picker/overrides and resets.
Spawns now cycles only between Mystic Jars and random event rooms. All 21 tabs
fit the existing three-row layout; page IDs and window size remain stable.
The new page requests `SnapshotContent.None`, uses the existing projectile
compatibility/recovery checks, and keeps the same commands and draft lifetimes.
No gameplay, saved-value or limiter-counting changes. See [limits](projectile-limits.md).

## Autonomous artifact cadence (`0.86.0`, 2026-10-22)

PvP Balance now starts its right scroll pane with an **Autonomous artifact hit
limit** toggle, exact seconds input, Apply and family Reset. The pane is titled
**Artifacts / Debuffs**; existing DoT controls follow below. Content grows from
700 to 812 pixels; window and viewport sizes remain unchanged. Values use shared
host commands, cached formatting, isolated drafts and ordinary scope cleanup.
See [settings and coverage](pvp-autonomous-artifacts.md).

## DoT duration control (`0.85.0`, 2026-10-22)

The top row of **PvP Balance → Debuffs / Dark Cloud** now sets the shared
Burn-family maximum duration (default 1.2s, range 0.05..10). Apply/Reset use
`/one friendlyfire dot-duration`; the hint names Burn, Plasma Burn and Blue Flame
so this lifetime control is not confused with the independent tick intervals.
Only the scroll content grows; the viewport/window stays the same size. Refresh
preserves unfinished input and avoids formatting unchanged values. Page, locale,
close and session transitions clear the draft; Apply clears only this field.
See [runtime semantics](pvp-dot-intervals.md#configurable-duration-cap-0850-2026-10-22).

## Perfect Guard controls (`0.84.0`, 2026-10-22)

**PvP Balance** has a Perfect Guard MP multiplier toggle and native-cost input
between the shield minimum and amplification controls. Default off/x2; the input
accepts `2` or `x2` and applies through the same host command as chat. When on,
Perfect Guard is exempt from the shield minimum; the EN/KO footnote makes this
explicit. Refill now appears only in **Rapid Hits**, accessible from the top-row
button. No new page, refresh loop or participant-detail snapshot is added.
Draft, host/compatibility availability and session cleanup use existing helpers.
See [cost semantics and commands](pvp-shield-mp.md#perfect-guard-multiplier-0840).

## Rapid-hit controls (`0.83.0`, 2026-10-22)

The **Rapid Hits / 연속 타격** tab beside PvP Balance separates hit detection
(window, full-damage rate, reduction exponent) from the shared damage allowance
(stored burst, continuous refill). Applied-value examples show the reduction
curve and base empty-to-full recovery time. There is no periodic reset timer.
Large-hit softening, native defenses and DoT schedules remain separate; the
budget also remains active when frequency scaling is off.

PvP Balance links to this page and retains general balancing, shield
minimum and amplification controls (the duplicate refill control was removed
in `0.84.0`). Twenty tabs use the same three-row layout;
the new page requests no participant details and uses existing refresh/draft
lifetimes. See [controls, defaults and commands](pvp-hit-frequency.md#controls).

## Dedicated PvP Balance tab (`0.82.0`, 2026-10-21)

Open `/one ui` and choose **PvP Balance / PvP 밸런스**, directly beside Combat.
General balancing occupies the left column without an inner scrollbar:
balancing toggle, rapid-hit page link, shield MP minimum, Perfect Guard multiplier and the
All Damage Amplified soft/hard limits. The right column contains all ten
**Debuffs / Dark Cloud** intervals and applicable **Keep schedule** switches.
Each effect has a full-width label; its list is 218 units tall rather than 82.

Combat keeps friendly fire, damage scale, lobby recovery, K/D/A reset, Revive
All, potion supplies and participant selection. These controls now appear
together without nested subsection tabs. Participant lists are taller across
Combat and the match modes.

The draggable window is now 760x560, with three rows of wider tab buttons and
a 600x250 logical content area at the existing 1.25 scale. Canvas fitting and
drag limits still accommodate smaller screens. No extra window, polling loop,
gameplay change or preset migration was introduced. The balance page requests
only base settings (`SnapshotContent.None`), avoiding participant-detail work.

Edits still use the shared commands. Apply commits only its own numeric draft;
routine refresh preserves unfinished edits, while page, language, close and
session transitions clear them. Existing host/compatibility and recovery rules
remain. EN/KO labels, build, panel geometry/lifetime, localization and installed
game contracts were checked; actual in-game rendering remains unverified.

## Earlier UI changes

Version `0.59.0` adds **DoT intervals / 지속 피해 간격** beside the Combat and
PvP balancing subsections. Four independent fields for Burn, Plasma Burn,
Frostbite and Poison fit in a 2x2 layout within the existing panel area. Each
accepts 0.05..10 seconds (two decimals), defaults to 0.75, and has its own Apply
button/draft. It uses `/one friendlyfire dot <effect> N`; these intervals apply
only with PvP balancing enabled. Refresh updates changed values without replacing
unfinished edits; page/language/session changes discard stale drafts. Shared
master/reset, K/D/A, Revive All and participant controls remain accessible.
See [interval semantics](pvp-dot-intervals.md).

Version `0.58.0` groups Combat settings into **Combat** and **PvP balancing**
subsections within the existing tab. The latter contains balancing, refill and
**Guard MP (%) / 방어 MP (%)** (see the current [strict shield floor](pvp-shield-mp.md)).
The guard input uses `/one friendlyfire guard-mp N` and applies live without
stopping a match or clearing scores. K/D/A reset, Revive All, master/reset and
participant controls stay accessible in both sections. Input drafts remain
independent; the existing page/session/language lifecycle clears stale drafts.
No window resize, new polling or extra global tab is needed.
See [guard behavior and live checks](pvp-balancing.md#guard-mp-cost-0580-2026-10-16).

Version `0.57.0` adds independent **Refill (x)** / **충전 (배율)** and
**Incoming (%)** / **받는 피해 (%)** controls to Play Tag. Both start enabled
at x2 and 75%. Each has its own numeric draft and Apply button; changing one
does not discard another unfinished edit. These changes apply live without
resetting the match. Refill requires PvP balancing; incoming reduction does not.
The five-row editor retains duration, Start/Stop, K/D/A reset and the shared
participant list within the existing window size. See [chaser rules](play-tag.md#chaser-advantages-0570-2026-10-16).

Version `0.56.0` adds **Play Tag / 술래잡기** with a next-match initial fuse
(3–3600 seconds, default 30), Apply/Reset, Start/Stop, shared K/D/A reset and the
same participant editor. The 16 tabs still fit the existing two rows; no new
window or per-frame UI controller is added. See [Play Tag](play-tag.md).

Tournament adds a separate **Tournament** tab with a round duration input
(10–3600 seconds, default 30), **Apply**, **Start match**, **Stop match**, and
**Reset K/D/A**. It shares Combat and Deathmatch's participant controls. Each
mode's Start/Stop controls operate only that mode; only one match can run at a
time. Changing the duration affects the next tournament. Saving a nondefault
Tournament duration uses preset v20; earlier presets keep the 30-second default.
Tournament progress and the temporary participant selection are never saved.
The 760×420 window keeps two tab rows, now with eight columns.

Version `0.45.1` exposes **Reset K/D/A** in both Combat and Deathmatch. Both
buttons work for regular hosted friendly fire and active deathmatches without
changing combat settings, selections, match duration or pending respawns.

Version `0.45.0` adds the same scrolling participant checkboxes, **Select All**
(모두 선택) and **Reset selection** (선택 초기화) to Combat and Deathmatch. Empty
selection includes everyone; any selection limits both features to selected
players. The list is temporary and never saved. See [participant rules](selective-friendly-fire.md).

Version `0.42.0` adds a **Deathmatch** tab with a duration input (seconds),
**Apply**, **Start match** and **Stop match**. The Combat
friendly-fire toggle reflects the match's effective state; touching it stops
the match. See [timing, results and recovery](deathmatch.md).

Version `0.39.0` implements the separate translucent, draggable window selected
from the [UI comparison](ui-revamp-investigation.md). Every feature panel is a
tab. Drag the title bar to reposition the window, and select a
stat from the dropdown. See [window and hotkey details](tabbed-settings-ui.md).
The compact layout is 760×420. `+` adds, `−` subtracts and `×` closes the window.
Short notes cover timing and exceptions; errors and compatibility warnings remain.

Added in `0.13.0`. In a hosted town or run, open the pause menu and click
**SephiriaOne** in its upper-right corner, or enter **`/one ui`** in chat.
Close with **×** or the game's existing Escape/cancel action. Configure an
optional shortcut in native **Settings → Keyboard controls → SephiriaOne**;
it starts unassigned. The panel uses mouse controls and keyboard entry;
full gamepad behavior has not been verified.

The host uses this panel to change settings for **all current and joining
players**. Guests can use the unmodified game. The panel itself is host-local;
effects use the same native synchronization as the existing commands.

Version `0.41.0` adds **Combat → Revive all players**, also available through
`/one reviveall`. This host action restores ready dead players at full HP through
native revival, including the host, while leaving living players and KDA totals
unchanged. It works with friendly fire off and unmodified guests. Runs already
settled by native game over cannot be resumed; see [recovery boundaries](friendly-fire.md#revive-all-recovery).

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
| Stats | Select one of the 27 supported visible stats with the dropdown or arrows, enter an amount, then Set, `+` or `−`. Reset selected or all stats. Current per-player values use the stat's displayed units. |
| Fountain | Set, add or subtract whole-number points for everyone; reset the addon adjustment. Shows current capacity and tracked contribution per player. |
| Choices | Select all, item, weapon or miracle. Set/add/subtract **extra** candidates, or reset selected/all categories. Shows current effective native extra-choice stats. |
| Resources | Select dice, inventory slots, talents, fruit-skewer budget or leaves. Set/add/subtract, `xN`, and selected/all resets. Dice/leaves show current balance separately from the next fresh starting allowance. |
| Presets | Save current applied settings, forget the saved copy, or refresh it from disk. Active intent and saved intent are shown separately. |
| Status | Current intent, player values, revisions, synchronization outcomes, native name status and fault details. Scroll for the complete readout. |
| Items | Default-off On/Off and Reset controls for given-item sale restrictions and owner-bound drops, including Fountain sharing. See [scope and native limits](item-restrictions.md); intrinsic costume-curse no-drop properties remain. |
| Combat | Friendly fire On/Off, a 0–300% damage slider/input with Apply damage, Reset (off/100%), Reset K/D/A, Revive all players, shared participant checkboxes and Select All/Reset selection. |
| Deathmatch | Set duration (10–3600 seconds, default 300), Start/Stop, Reset K/D/A, remaining time and the shared participant selection controls. |
| Tournament | Set each round's duration (10–3600 seconds, default 30), Start/Stop, Reset K/D/A, current round status and the same participant selection controls. Selections are captured on start; changing selection ends the current match. |
| Play Tag | Set/reset the next match's initial fuse (3–3600 seconds, default 30), Start/Stop, Reset K/D/A and shared participant controls. Transfers shrink the fuse; one explosion ends the match. |
| Spawns | Choose Mystic Jars or random events. Apply a chance or native-rate multiplier using the selected feature's existing limits; Reset restores native. Existing generated results are preserved. |
| Costumes | Independent Bat HP-steal reduction and Collin starting-artifact toggles, resets and compatibility readouts. |
| Rabbit | Independent On/Off controls for Wing-Eared Rabbit infinite HP potions, nearby potion healing, MP cost, Survival rank-5 suppression and random non-HP/MP potions on level-up. Sample is excluded from all HP-potion options and keeps native behavior. Enter 0..10000 MP and press Set & on to choose a fee; Off remembers it. Reset restores 10 MP and disables all five options. Shows active state and separate drink/level-up compatibility. |
| Merchant | Select Wandering Merchant, Papyrus or Taz. Each has spawn On/Off, a separate Guarantee On/Off (default on), chance 0..100%, first main dungeon stage 1..1000, run limit 0..1000 (0 = unlimited), and Reset selected. Enter a number, then choose its action. Guarantee off leaves chance rolls active and releases the reserved cap slot. Multiple types can share a floor. Added merchant base HP uses main-stage multipliers 1/2/4/6/7/8 for stages 1–6 (then stage number + 2, increasing by ×1 per stage; maps within a stage share the factor), retaining native HP bonuses and no crime penalty. |
| Updates | Local automatic-check toggle, check now, update installation button and release/status details. Explicit installation still requires a full game restart to activate. |

On/Off settings appear as checkboxes. Actions only run on deliberate input.
Typing, switching tabs and refreshing observations
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

1. Open through chat, pause launcher and the configured hotkey, close with
   Close/Escape, and reopen repeatedly. Drag and resize the window.
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
