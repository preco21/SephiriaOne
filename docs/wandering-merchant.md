# Extra hostile Wandering Merchant

Added in `0.20.0`, disabled by default. Only the host needs SephiriaOne.
Since `0.24.0`, this is the `wandering` type in the
[independent merchant catalog](merchant-variants.md), alongside Papyrus and Taz.
Commands below remain aliases for Wandering only; its Off/Reset do not change
other types. Each enabled type has its own conditions and guarantee. Since
`0.25.0`, `/one merchant guarantee on|off` controls the original type's guarantee
separately; default on. Off leaves chance rolls active and releases its reserved
cap slot. The Merchant panel exposes the same option for every type.
Open `/one ui` → **Merchant**, or use chat:

```text
/one merchant chance 25
/one merchant guarantee on
/one merchant on
/one merchant status
/one merchant off
/one merchant reset
/one save
```

`chance` accepts a whole-number percentage from 0 to 100; default **25%**.
Changing the chance preserves the on/off setting. Reset disables the feature and
restores guarantee on and 25%. Save retains the toggles and chance for future hosted
sessions; custom settings are remembered even while off. Simple merchant settings
use v7, typed conditions use v8, and guarantee off uses v9. All earlier presets
remain supported and keep guarantees on. Run history is never reset by these options.

## Encounters and balance

- With guarantee on, **each new run** gets one guaranteed encounter scheduled across
  its potential eligible normal floor positions. The first floor remains eligible,
  but is no longer forced. Other floors roll the configured chance before or after
  that position; an early chance encounter does not consume the scheduled guarantee.
  At most one addon Wandering Merchant spawns on each floor. Other types can share
  that floor when their own rules and safe placement allow it.
- **0%** means only the guaranteed encounter when enabled, or none with guarantee
  off; **100%** means every eligible floor subject to the configured cap.
  Chance changes apply to floors that have not yet rolled. Turning the option on
  during a run selects from current/future progression. It does not populate passed
  main-route floors just because their generators remain loaded. Optional stages
  outside that finite schedule retain their existing chance-only behavior.
- Since `0.28.2`, added merchant **base HP uses the main dungeon stage number**:
  stage 1 = ×1, stage 3 = ×3. Maps within a stage share that factor and the same
  `from` threshold; earlier versions incorrectly counted individual maps. Native
  stage and multiplayer HP bonuses still apply. Optional rooms use the current main stage; unknown progress
  uses ×1. This is computed only at spawn and also applies to Papyrus and Taz.
  The added merchant starts hostile. Attack damage follows native scaling.
  Its original combat AI, attacks and native stock/loot container are reused.
- Boss floors, single-room/special generators, towns, lobby, training and hidden
  or pocket floors are excluded. Rooms must allow normal monsters and have a clear,
  walkable spawn point away from other stock containers. Corridors are excluded.
- Killing this specific added merchant applies **no NPC-crime or negotiation
  penalty**. Its damage callback also cannot change shared faction relationships.
  Natural merchants retain their usual behavior and penalties. Existing crime
  debuffs are never removed or changed.
- Off/reset stop future spawning. Already spawned extras stay alive and retain
  their exemption. Their native loot becomes accessible after death.

Version `0.22.1` removes the previous 3× addon HP boost for newly spawned merchants.
Native floor and multiplayer scaling still apply, and merchants start at full HP.
Existing actors are not rescaled; saved toggle/chance settings are unchanged.
Version `0.26.0` replaces the flat ×1 addon factor with the floor-based rule above.

The guarantee requires an eligible room and valid native game state. If native
initialization fails, the partial spawn is cleaned up and that floor is not retried;
the guarantee remains pending for another eligible floor. If every remaining floor
is excluded or unsafe, the mod cannot create an eligible encounter.

Scheduling uses the active scenario's native stage assets. Choice-stage branches
share a progression depth, so the selection follows the branch players actually
take. Grassland's mission board uses the order of distinct requested/visited
missions, including unsupported missions in the progress count, rather than a
random mission ID that players might never choose. Safe entrances, boss-only steps
and unsupported prefab pools are not candidates. Unknown route structures do not
fall back to a forced first-floor encounter.

Future branches and special-event replacements can change actual eligibility.
Selection is random over potential progression positions, not exactly uniform over
all rooms ultimately visited. If the selected floor is unsafe, unsupported or
passed while disabled, the guarantee carries forward to the next eligible floor.
Stopping a run early can therefore mean never reaching the scheduled encounter.

## Synchronization and lifetime

The SDK's completed floor-generation notification runs on the host after geometry,
normal spawners and travelers have been generated. Explicit command/preset changes
also check current loaded progression. There is no per-frame room scan and no
join-driven spawn. Native floor allocation is requested travel; generation completes
before the player's actual arrival. Native travel histories seed progress when
enabling the option midway or continuing a save.

The host stores `SephiriaOne.MerchantFloor.<guid>` and
`SephiriaOne.MerchantEncounter` in the native current-run save. These record consumed
floor rolls and fulfillment of the guarantee. Versioned
`SephiriaOne.MerchantSchedule.v1.*` keys retain the selected progression position
and furthest observed progress; mission ordinals are also stored in the run.
Chance-based extras do not mark the guarantee fulfilled. Old saves with the
encounter marker already set keep it fulfilled after upgrading; existing consumed
floor markers are never reopened. Duplicate notifications, toggle cycles,
guest re-entry, regenerated/revisited floors and saved-run continuation cannot reroll
or duplicate the encounter. A fresh native run save resets this state. The addon does not
force a save; these keys follow the game's normal save cadence.

Both the actor and its dedicated new stock container use existing network prefabs
and native SyncVars. Joining/rejoining guests receive them through the game's normal
spawn synchronization, without addon packets, network scripts or assets. Exact actor
references identify exemptions; reused network IDs, matching names or matching
prefab types do not qualify. Floor teardown, host-scope replacement and unload clean
up only the addon-owned objects. Unload/reload does not replenish consumed encounters.

The stock container is instantiated and tracked before calling native `SetSocialID`.
That native method otherwise adopts the nearest safe within ten world units, so
placement and initialization both check container isolation. A unique social ID
avoids natural NPC talk/quest lookup collisions. Dialogue is disabled for the combat
encounter. Native loot interaction supports this ID through its existing fallback.

The runtime selects an existing faction that is hostile to Player in the current
scenario. `Undead` is checked first, then `Pillagers`; this avoids assuming Undead is
hostile in the Mole scenario. No global faction relations are written by the addon.

## Verification and remaining manual checks

Installed-game investigation used Sephiria 1.0.33, `Assembly-CSharp.dll` SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
The combat prefab is `BabaMerchant_BattleHard` (native network asset ID 665040392),
with stock settings from `Merchant_Papa` and the native `Mat_TravelerMerchant` safe.

Automated checks cover shared parsing/persistence, host commands, lifecycle and
ownership fixtures, and installed IL contracts for crime, damage/death ordering,
stock initialization, HP scaling, completed generation and guest replication.
Both production build configurations use `DeployMod=false`.

Scheduling regressions cover first/middle/last selection across run seeds, chance
extras on both sides, 100% chance at the selected floor, copied-save continuation,
legacy save markers, late enablement, reordered loaded floors, off/on and guest
re-entry, unsafe rooms and partial network-spawn failures. Existing merchant actor,
room placement, settings, shared synchronization and native compatibility checks
remain in place. A destruction-failure regression verifies that native floor-cleanup
references and exact-actor penalty protection remain available until cleanup succeeds.

Version `0.23.0` verification: 362 merchant runtime checks across 44 scenarios,
29 route adapter checks, 34 room-placement checks and 782 shared runtime checks
pass. Portable policy/localization and installed-game compatibility checks pass.
Debug and Release builds have zero warnings/errors. Independent review found no
remaining blocking defects after the early-initialization recovery fix.

Live Unity gameplay and multiplayer rendering are not exercised by these tests.
Before relying on it in a long run, verify:

1. Enable with 0% across several fresh runs: one encounter on a varying floor when
   the full eligible route is reached, with the first floor still possible. Repeat
   with 100% and confirm at most one per floor. Test a Grassland mission-board run.
2. Kill an extra with an unmodified guest: negotiation/crime remain unchanged,
   native stock is lootable, and natural merchant hostility remains unchanged.
3. Kill a natural merchant separately and confirm the game's normal penalty still applies.
4. Compare native versus added merchant HP under floor and multiplayer scaling;
   test the Mole scenario's hostile-faction fallback.
5. Reconnect a guest, revisit a floor, toggle off/on, and continue a saved run;
   confirm no duplicate encounters or foreign stock-container ownership.

No deployment script was run for this change.
