# Wing-Eared Rabbit level-up potions

Added in `0.28.0`, off by default. The host can enable one random native potion
for every level earned while Wing-Eared Rabbit (`HolyRabbit`) is equipped:

```text
/one rabbit level-up-potion on
/one rabbit level-up-potion off
/one rabbit status
/one rabbit reset
/one save
```

The Rabbit tab in `/one ui` has the same toggle. Reset disables every Rabbit
option and restores the default MP fee. Save explicitly retains the current
settings for future sessions; enabling this flag requires preset v10. v1-v9
remain supported and leave this option off. Disabling it retains the oldest
applicable preset version. Earned potions remain ordinary inventory items when
the toggle is disabled or the costume changes; they are not starting equipment.

## Potion pool

The [requested potion index](https://sephiria.world/potions) was cross-checked
against the installed game's item names and prefab effect data. All 19 entries
below have equal probability; draws are independent, so duplicates are possible.

| Native IDs | Potions |
| --- | --- |
| 28, 29, 30, 31 | Nebbiolo's Stubbornness, Ugni Blanc's Mist, Tempranillo's Passion, Malbec's Depth |
| 33, 34 | Large Dice Potion, Small Dice Potion |
| 35 | Dew of Dawn |
| 38, 39 | Potion of Awakening, Mushroom Soup |
| 40, 41, 42, 43 | Sanctity of Trappist, Smoke of Märzen, Crystal of Aventinus, Morning of Gose |
| 46 | Solera's Serendipity |
| 47, 48, 49, 50, 51 | Sagrantino's Endurance, Poulsard's Margin, Madiran's Poise, Grisette's Lightness, Gueuze's Ensemble |

HP/MP restoration, HP increases and lifesteal are excluded. Random-stat potions
32/44 are also excluded because their native pools can roll MP regeneration.
Hidden native potions 35/46 are included, following the requested *all other
potions* scope. Enchantment and combo potions retain native indirect effects;
these can affect HP/MP through the resulting equipment/combo rather than through
a direct potion stat grant. No new item definitions, assets or network messages
are introduced.

## Native synchronization and lifecycle

Only the earned-level call in `LevelController.LocalAddExp` is wrapped, after
the native synchronized level increment. A multi-level XP award grants one
potion per level, after each successful native level-up callback. Enabling the
option does not grant rewards for levels already earned. Initialization,
restoring saved levels and reconstructing pending reward choices do not replay
past grants. Native catch-up XP after reconnecting can earn new levels normally.

Policy is read from the shared session service at use time. Current costume,
avatar, connection owner/readiness, inventory, run generation and floor are
checked before and after native callbacks and after entering inventory write
permission. New connections use the current policy without per-player addon
markers. Stale operations are discarded rather than written to a replaced
avatar or connection. No polling, catch-up reward ledger or per-frame work is
added.

The host uses `GridInventory.LocalAddItem` with a generated native instance ID,
quantity one and native notifications. Existing inventory write permission is
borrowed; only a scope opened by this feature is closed by it. This matters
because the game's permission class is not reentrant. Inventory and temporary
inventory already replicate to stock guests.

If native addition explicitly returns false (for example, full potion storage),
one item is placed in native temporary inventory, as with native pocket-dimension
purchases. The native inventory/reward UI later drops temporary items when it
closes. **Temporary inventory is not saved**; this is stock transient overflow,
not a durable delivery queue. Exceptions never trigger a fallback or retry,
because an inventory mutation may already have succeeded.

Level-up compatibility is independent of potion-drinking compatibility. Database
readiness checks all 19 item IDs, localization keys, potion categories and effect
types; selection rechecks the chosen entry. An invalid catalog or hook disables
only this reward feature. A runtime failure logs once and stops further addon
rewards while retaining native leveling. `/one status` and the Rabbit panel show
unavailability; off/reset remain available.

Host command, panel and costume-description text have EN/KO catalog entries.
Guests receive the native items and notifications; the added description text is
host-local, consistent with existing Rabbit options.

## Verification

Source-linked Harmony tests cover batched/nested earned levels, option/costume
changes, restored choices, reconnects, stale connection/run/floor/inventory,
permission callbacks and nested scopes, full storage, exceptions after a native
write, missing catalog members and unload. Existing command/preset, session,
description and localization suites also cover the new flag. Installed-game IL
checks verify the precise earned-level boundary, preservation of native code,
server XP routing, restore exclusions and native inventory serialization.

Live Unity/gameplay verification is still required:

1. Enable on the host; level up a Rabbit host and an unmodified Rabbit guest.
   Check one native potion each, and no grant to another costume.
2. Gain several levels in one XP award. Switch costumes and disable/re-enable;
   confirm there are no retroactive rewards.
3. Fill potion storage, level up, and close the native inventory/reward UI to
   check its temporary-item drop behavior.
4. Rejoin, reload a saved run with pending level choices, then gain a new level.
   Confirm old levels do not replay rewards and the new level grants once.
5. Save the option, restart and host again; verify the current setting and Korean
   panel/tooltip. No deployment or live gameplay test was performed by the agent.
