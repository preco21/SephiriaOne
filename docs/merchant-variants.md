# Independent hostile merchant variants

Implemented in **0.24.0**. Only the host installs SephiriaOne; guests use the game's
existing assets, combat controllers, inventory networking and RPCs.

| Command ID | Type | Native combat | Native stock |
| --- | --- | --- | --- |
| `wandering` | Existing Wandering Merchant (Papa) | `Unit_BabaMerchantHard` | Merchant-union vendor |
| `papyrus` | Papyrus / 파피루스 | `Unit_Soldier`, melee attacks | Small vendor |
| `taz` | Taz / 타즈 | `Unit_TurtlePotion`, shell and projectile attacks | Potion vendor |

Each type defaults to **spawns off, guarantee on, 25% chance, first eligible floor
1, unlimited per run**. Version **0.25.0** adds the independent guarantee toggle.
Existing Wandering settings are retained. Since **0.26.0**, all three types multiply
base HP by a floor-based factor while retaining native stage and
multiplayer HP/attack bonuses. Added actors are
hostile, cannot talk and receive the same exact-instance crime exemption. Natural
merchants remain unchanged.

## Commands and panel

Open `/one ui` → Merchant and use the arrows to select a type. The number field is
shared by **Set chance**, **Set first floor** and **Set run limit**. Switching types
clears unsubmitted input. On, Off and Reset selected apply only to that type.
The **Guarantee** row has its own On/Off controls; current state appears on the right.

```text
/one merchant papyrus chance 10
/one merchant papyrus from 3
/one merchant papyrus limit 2
/one merchant papyrus guarantee off
/one merchant papyrus on
/one merchant taz chance 15
/one merchant taz on
/one merchant status
/one merchant papyrus status
/one merchant papyrus off
/one merchant papyrus reset
/one save
```

- `guarantee on|off`: default on. Off disables forced encounters, leaving ordinary
  chance rolls active. This does not enable or disable the type's spawn toggle.
- `chance`: whole-number 0..100 percent on eligible floors without a forced encounter.
- `from`: 1..1000, the main dungeon stage number. Since **0.28.2**, every map
  within stage 1 is below `from 2`; map visits no longer advance this number.
  Grassland missions share their stage number. Safe/boss rooms remain excluded
  from spawning, while their main stage still occupies its normal stage number.
  Optional maps inherit the current main-route stage.
  A minimum of one also allows otherwise eligible unknown-route floors.
- `limit`: 0..1000 successful spawns of this type per run; zero means unlimited.
  Guaranteed and chance encounters both count. While an enabled guarantee remains
  reachable, one slot is reserved for it. A limit of one then yields only that guarantee.
  Guarantee off releases the slot so chance rolls can use the full cap.
  Unsupported/exhausted routes with no scheduling opportunity retain chance-only
  spawning up to the full cap.
- `off` preserves the guarantee, rate and conditions. `reset` restores this type's
  defaults, including guarantee on while spawns remain off.
  Neither operation removes existing actors or replenishes consumed run state.

Old `/one merchant on|off|reset` and `/one merchant chance N` commands still address
Wandering Merchant. `/one merchant guarantee on|off` also addresses only Wandering.
`/one merchant status` lists all types, including each guarantee's state. Settings use the
same host authorization and transaction service as the other commands. Use
`/one save` to persist them for future sessions; they are not saved automatically.

## Independent spawning and guarantees

With guarantee on, each enabled type gets its **own** random guaranteed encounter
per run, within its current conditions. At 0% there are only guaranteed encounters;
with guarantee off and 0% there are no encounters. At 100% every
eligible floor can contain that type, subject to its cap. At most one of each type
spawns on a floor; other types are evaluated separately and can coexist there.

Targets, chance rolls, placement and actor RNG use stable type-specific salts.
Adding another catalog entry cannot change an existing type's seed. A chance extra
does not complete the guarantee. Chance misses, successful spawns and failed
reserved attempts are remembered per type/floor and cannot be rerolled by commands,
presets, reloads or guest re-entry. A new run starts fresh.

Guarantee off pauses a pending target and prevents selecting a new one. Route
progress still advances. Re-enabling preserves the target (or selects among the
remaining eligible positions if none exists); a missed target can carry forward
to a later unused eligible floor. It cannot reopen a consumed floor roll,
replenish a completed guarantee, or exceed a cap spent while the guarantee was off.
Existing actors retain their crime exemption regardless of the guarantee toggle.

Use `/one save` to retain the option for future sessions. A nondefault guarantee
uses preset v9; earlier presets retain guarantee on. Run history remains separate
from preset intent. Neither changing nor reloading a preset clears that history.

Changing the earliest floor does not reroll an existing target: the pending
guarantee waits until current conditions allow it. Lowering the cap never deletes
existing actors; it stops further spawns. Raising it can allow future unused rolls.
Spawning remains event-driven, with no new per-frame scans or join-time spawn path.

Existing room restrictions still apply: no boss-only, safe, training, hidden,
pocket or unsupported-generator rooms. Each new actor and stock container must
have a safe position more than ten units from every existing safe. Crowded or small
rooms can therefore reject an additional type. A failed type is rolled back alone;
other types continue. If its guarantee is still pending it can carry forward to a
later eligible floor. Runs ended early or without a remaining safe opportunity
cannot be forced to contain the encounter.

Since 0.26.1, candidate placement checks read the live native safe registry without
calling the native nearest-safe lookup, which emits debug logs for each nearby
safe. Distance, candidate order and live occupancy rules are unchanged. Final
stock ownership validation still uses the native lookup. Missing registry data
skips placement until native initialization is ready.

The original type keeps its pre-0.24 run keys. New types use
`SephiriaOne.MerchantVariant.v1.<id>.*` for consumed floors, count, completion and
schedule. Old saves had no success counter, only consumed rolls: on first use,
Wandering's historical consumed rolls are conservatively counted toward a newly
configured cap. This can include old misses/failed attempts; starting a fresh run
gives an exact success count. New-version counts include successful spawns only.
The game saves these values on its normal cadence; the addon does not force saves.

## Floor-based health

Since **0.28.3**, each added merchant uses the following base-HP multiplier at
spawn, retaining the normal native stage and multiplayer percentage bonuses:

| Main dungeon stage | Base-HP multiplier |
| --- | --- |
| 1 | ×1 |
| 2 | ×2 |
| 3 | ×4 |
| 4 | ×5 |
| 5 | ×7 |
| 6 and later | ×8 |

This applies to guaranteed and chance-based encounters of every type without a
separate setting or preset migration.

The floor number is the same main dungeon stage number used by `from`. Maps and
Grassland missions within one stage all use the same factor. Main-stage ordinals
come from the race's ordered main stages, so loading an optional side stage does
not shift them. A playable lobby uses ×1. Optional rooms inherit the current
main-route stage; unsupported or unknown progress falls back to ×1. A
delayed guarantee uses the floor where it actually spawns, not its original target.

For example, 2,500 base HP in main stage 3 becomes 10,000 before native
percentage bonuses. With a total native +230% bonus it becomes 33,000 final HP.
Attack and defense keep their existing scaling. The native synchronized base-HP
setter and full heal propagate to unmodified guests without new network fields.

This is a one-time spawn calculation: settings changes, player re-entry and later
floor transitions do not rescale or heal existing actors. Prefabs and natural
merchants are unchanged. Invalid or overflowing scaled HP aborts that individual
spawn before the increased HP is written; partial actors are cleaned up and an
unfulfilled guarantee can continue on a later eligible floor. No per-frame work
or new session state is added.

## Restart and numbering correction (0.28.2)

Earlier versions counted each eligible map as another floor. The host log showed
merchants during native stage 1 after additional map visits, explaining why
`from 2` could permit them and why the HP factor could already reach ×3.
Stage numbering now controls both conditions and HP, including guarantee candidate
selection and reserved cap slots. Per-map chance rolls, chronological map positions
and saved guarantee targets retain their existing identities. An old pending target
below the new minimum waits for the next eligible unused map; it is not rerolled.
Completed guarantees, counts and consumed rolls are not reset by settings changes.

Native restart briefly exposes a fresh `CurrentRun` before reloading the reused
dungeon. The addon now requires the save's Seed/CurrentGame to match the loaded
dungeon before it observes maps or writes merchant state. This prevents a settings
refresh in that window from importing the previous run's progress. New runs rebuild
their route and per-type schedules; ordinary reloads retain their saved state.

Native HP initialization was audited: it does not add the stage/co-op bonuses a
second time. Those bonuses remain, so final HP can exceed the floor factor alone.
Each successful added spawn now logs its main stage, map position, saved target,
guaranteed/chance result, HP factor and base/final HP for subsequent diagnosis.

The installed asset audit covered all 26 `RaceEntity` records (25 in
`resources.assets` and one in a shared asset file). Normal
race arrays follow their main-stage progression; alternate races may start at
Grassland or contain only a quest stage. The playable Mole scenario has no main
stage array and retains factor 1. None of those arrays has null, duplicate or
repeated-lobby entries; the adapter also handles those defensively, matching native
name-based load deduplication. Side stages do not advance the main-stage factor.

## Native investigation

Sephiria 1.0.33 provides all three exact avatar prefabs in both native network
prefab lists: `BabaMerchant_BattleHard` (665040392), `MouseMerchant` (4162572897)
and `Turtle_Potion` (2656760657). Papyrus's attack and Taz's three projectiles are
also registered, with client-visible network identities. All three inherit the
existing `UnitAvatar` / `UnitAI_NewBasic` paths used by the shared ownership hooks.

Papyrus and Taz retain native death rewards: 100 EXP, half their native starting
600 money, and their own avatar-inventory drop path. Sale stock is separate. Taz's
potion-looking attacks use native damage/projectile logic; they do not drink,
heal or consume sale stock. Native inventory generation can use Unity's RNG, so
stable encounter seeds do not promise deterministic contents for every vendor.

Taga and Rona were excluded: their mage templates lack `SkillController_NPC`,
which the AI dereferences in combat. Thunder's villager AI does not attack.
No components or network scripts are injected to work around these limitations.

## Adding future variants and conditions

1. Verify a native template's combat, death, stock and registered network assets.
2. Add one `MerchantDefinition` entry in `MerchantCatalog`: stable lowercase ID,
   English label, SocialID, exact avatar controller name, unique stable seed salt,
   guarantee policy and default rate. Keep existing IDs/salts unchanged.
3. Add matching EN/KO label entries. Parser, panel selection, snapshots, presets,
   save-key scoping and the runtime loop discover the catalog entry automatically.
4. For additional native eligibility rules, supply a pure `Condition` predicate
   over `MerchantSpawnContext` (main-stage number, successful count, native difficulty,
   stage name). Built-in earliest-floor/cap checks are always composed with it.
   Conditions must not mutate game state. They also apply to guaranteed encounters;
   a target with an unmet rule remains pending for another eligible floor.
5. Extend native compatibility and fixture tests. A new editable setting requires
   its settings/parser/preset/UI representation, but no changes to actor spawning,
   stock ownership, native scaling, rollback or teardown.

`MerchantRuntime` owns orchestration; `MerchantSchedule` selects guarantees;
`MerchantRunState` owns per-type persistence; `MerchantSpawnRules` composes
conditions; `MerchantRooms` locates safe placements. The catalog contains data and
rules, not network operations.

## Validation

Automated coverage includes independent zero/100% behavior, salted guarantees,
same-floor stock ownership, earliest floors, caps and reserved guarantee slots,
unsupported-route chance caps, copied saves, legacy markers, re-entry/toggles,
failure isolation and native crime/replication/combat contracts. Tests do not run
Unity gameplay or verify live visual balance. Test several full runs with an
unmodified guest, including a floor with all three types and a saved-run continuation.

The 0.24.0 baseline verified 459 merchant runtime checks across 57 scenarios, 45
room-placement checks, 31 route checks, 119 settings checks, 811 shared runtime
checks, 1038 catalog checks and the installed-game contracts. Both production build
configurations reported zero warnings/errors. The five-player synchronization
performance fixture remains at zero allocated bytes per tick; this is an addon
fixture measurement, not a live-game frame-rate benchmark.

The guarantee-toggle regressions cover zero chance with guarantees off, mixed
type settings, released cap reservations, cap exhaustion before re-enabling,
pending/completed targets, copied run saves, re-entry, delayed first enablement,
host authority, detached snapshots, v1-v8 compatibility and v9 persistence.
Live checks: open the Merchant page in both languages, change each guarantee,
save and restart, then exercise off/on during a run with an unmodified guest.

Version 0.26.0 verifies 551 merchant checks across 75 scenarios, 31 route checks,
1061 bundled catalog checks, the portable suite and installed-game HP/replication
contracts. Debug and Release builds pass with zero warnings/errors. Health cases
cover each type on floors 1/2/3/7 with one and five players, native bonuses, reused
prefabs, optional/unknown routes, delayed guarantees, repeated refresh/re-entry,
copied run state and overflow cleanup. Live Unity gameplay still needs testing:
compare the same type across floors, inspect host/guest health bars, and reconnect
without changing an existing merchant's HP.

No deployment script was run.

Version 0.28.2 passes 562 runtime checks across 78 scenarios, 1,115 real-route and
scheduler checks, 63 placement checks, 834 shared session checks, 48 localization
tests, 1,103 catalog checks and the portable/installed-game compatibility suite.
The new regressions first failed for map-based numbering and the fresh-save/old-map
restart overlap. They now cover consecutive runs on one dungeon manager, stage-1
multi-map HP/minimums, stage-2 guarantees, mid-run edits, retained targets and
duplicate/null stage entries. Native IL checks pin the save replacement and
restart/load identity boundary. Live gameplay and deployment were not performed.
