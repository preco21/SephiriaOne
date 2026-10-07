# Wing-Eared Rabbit starting artifact

Added in **0.27.0**. Wing-Eared Rabbit (`HolyRabbit`) starts with the native
**Crest of the Iron Wall** (`1314`) alongside its existing **Blessing** (`3031`).
This is always part of that costume while the addon is loaded; there is no
command, preset value or extra toggle. Rabbit potion settings remain independent.

The Crest keeps all native behavior, including legendary rarity and its
**Sword and Shield weapon requirement**. Equipping another weapon does not enable
the Crest's effects. No item definition, balance values or weapon restrictions
are changed.

## Costume ownership and multiplayer

The addon appends the existing item entity to the costume's `startingItems` at
`OnAllDatabasesReady`. This timing matters: the installed game loads costumes
before items. Registration checks both the item ID and the language-independent
name key, `Item_SwordShieldGrowth_Enhanced_Name`. Missing/mismatched data leaves
the costume untouched and logs a diagnostic.

The game then handles the item exactly like its other costume starting items:

- `PlayerAvatar.UpdateCostumeData` grants it on the server and records the
  returned instance ID in `costumeStartingItemInstanceIDs`.
- Switching costumes removes the recorded instance via `RemoveStartingItem`,
  including inventory, temporary/pending additions, sub-bag and dropped-item
  cleanup. Switching back creates the costume's starting items again.
- Removal targets the costume-owned instance, not every Crest with item ID
  1314. Independently obtained copies are left to normal game rules.
- Run restart clears the run inventory and calls `RestockStartingItem` after
  player initialization. It uses the current costume-owned list.
- Joining players use the native costume initialization and inventory
  synchronization. There is no addon state keyed by a previous connection.

Only the **host** needs the addon for guests to receive the artifact. Guests
already have the native item definition, prefab and effects. The host's costume
preview automatically includes its icon; an unmodified guest's costume-selection
preview still uses its own stock data. Actual inventory and effects are native
networked state.

Saved runs keep their native inventory restoration rules; this is a starting
artifact, not a retroactive grant into an already saved run. No saved inventory
is edited and no repeated grants are sent during play.
Native resume rebuilds the costume's starting-item list separately from saved
inventory instance IDs. Normal costume/preset edits are blocked in the dungeon;
the next run restart clears the saved copies before restocking. No additional
ownership guarantee is provided for debug commands or other mods that force a
costume change inside a restored dungeon run.

## Lifecycle and performance

Duplicate database-ready callbacks reuse the existing entry. Other costume
templates and source arrays are preserved. Addon unload restores its template
edit, retaining later edits from other addons. Already-created item instances
remain under the game's costume ownership and removal rules.

There is no new per-frame work, player scanning, Harmony patch, custom RPC,
per-player dictionary or reconciliation feature. Registration allocates one small
array and relies on the native grant/removal pipeline thereafter.

## Verification

Inspected the installed game and assets on 2026-10-07. The native item asset is
`1314_SwordShieldGrowth_Enhanced`, with prefab
`Charm_SwordShieldGrowth_Enhanced`; its `Charm_GrowthGuard` retains the native
weapon restriction. No decompiled game code/assets are included in the repository.

Automated checks cover registration and template ownership, duplicate callbacks,
pre-existing Crests, other costumes, missing/mismatched assets, database
replacement, unload/reload, and later template edits by other addons. Installed
assembly checks cover database timing, server grant/instance tracking, costume
switch cleanup, pending/full-inventory cleanup paths, restart restocking, native
starting-item synchronization and host preview integration.

Verification on 2026-10-07/08: Debug and Release builds passed with zero warnings;
27 artifact registration checks, 823 command/session checks, 167 potion-hook
scenarios, 12 localized potion alerts and 41 costume-description checks passed,
along with the portable and installed-game compatibility suites. Run the focused
fixture with:

```powershell
dotnet run --project tests/SephiriaOne.RabbitArtifactTests -c Release -p:DeployMod=false
```

These checks inspect native contracts and exercise linked-source fixtures;
they do not replace a live Unity/multiplayer test. Deployment remains disabled.
Before release testing, equip/switch away/back in the lobby, repeat a run,
reconnect an unmodified guest, and verify that an independently obtained Crest
survives the costume switch while the starting copy is removed.
