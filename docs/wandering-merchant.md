# Extra hostile Wandering Merchant

Added in `0.20.0`, disabled by default. Only the host needs SephiriaOne.
Open `/one ui` → **Merchant**, or use chat:

```text
/one merchant chance 25
/one merchant on
/one merchant status
/one merchant off
/one merchant reset
/one save
```

`chance` accepts a whole-number percentage from 0 to 100; default **25%**.
Changing the chance preserves the on/off setting. Reset disables the feature and
restores 25%. Save retains the toggle and chance for future hosted sessions; a
custom chance is remembered even while off. Presets with merchant settings use v7;
v1–v6 presets still load and default this feature to off/25%.

## Encounters and balance

- The first eligible normal dungeon floor **of each new run** gets a guaranteed
  encounter while enabled. After that, each eligible floor rolls the configured
  chance, with at most one added merchant on that floor.
- **0%** means only the guaranteed encounter; **100%** means every eligible floor.
  Chance changes apply to floors that have not yet rolled. Turning the option on
  during a run also processes already generated eligible floors.
- The added merchant starts hostile and has **1× normal maximum HP**, including
  the native floor and multiplayer scaling. Attack damage follows native scaling.
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

The guarantee requires an eligible room and valid native game state. If native
initialization fails, the partial spawn is cleaned up and that floor is not retried;
the guarantee remains pending for another eligible floor. If every remaining floor
is excluded or unsafe, the mod cannot create an eligible encounter.

## Synchronization and lifetime

The SDK's completed floor-generation notification runs on the host after geometry,
normal spawners and travelers have been generated. Explicit command/preset changes
also check loaded floors. There is no per-frame room scan and no join-driven spawn.

The host stores `SephiriaOne.MerchantFloor.<guid>` and
`SephiriaOne.MerchantEncounter` in the native current-run save. These record consumed
floor rolls and the first successful spawn. Duplicate notifications, toggle cycles,
guest re-entry, regenerated/revisited floors and saved-run continuation cannot reroll
or duplicate the encounter. A fresh native run save resets both. The addon does not
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

Verification results: Debug and Release builds have zero warnings/errors; 43
merchant settings checks, 729 shared runtime checks, 63 merchant runtime checks
across 31 scenarios, and 34 tests of the actual room-placement code pass. Existing
starting-resource (71), resource-budget (66), Rabbit potion (134), Rabbit description
(35), disconnect (14), portable-policy and installed-game compatibility suites pass.
Independent review found no remaining blocking implementation defects. A destruction
failure regression verifies that native floor-cleanup references and exact-actor
penalty protection remain available until cleanup succeeds.

Live Unity gameplay and multiplayer rendering are not exercised by these tests.
Before relying on it in a long run, verify:

1. Enable with 0%: one encounter on the first eligible floor, none on later floors;
   repeat with 100% and confirm at most one per floor. Restart a run for a fresh guarantee.
2. Kill an extra with an unmodified guest: negotiation/crime remain unchanged,
   native stock is lootable, and natural merchant hostility remains unchanged.
3. Kill a natural merchant separately and confirm the game's normal penalty still applies.
4. Compare native versus added merchant HP under floor and multiplayer scaling;
   test the Mole scenario's hostile-faction fallback.
5. Reconnect a guest, revisit a floor, toggle off/on, and continue a saved run;
   confirm no duplicate encounters or foreign stock-container ownership.

No deployment script was run for this change.
