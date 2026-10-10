# Wing-Eared Rabbit starting artifacts

Wing-Eared Rabbit (`HolyRabbit`) starts with its native **Blessing** (`3031`),
plus **Crest of the Iron Wall** (`1314`, added in **0.27.0**) and
**Blood of Obrus / 오브러스의 피** (`1171`, added in **0.46.0**).
These are always part of that costume while the addon is loaded; there is no
command, preset value or extra toggle. Rabbit potion settings remain independent.

The Crest keeps all native behavior, including legendary rarity and its
**Sword and Shield weapon requirement**. Equipping another weapon does not enable
the Crest's effects. No item definition, balance values or weapon restrictions
are changed.

## Costume ownership and multiplayer

The addon appends the existing item entities to the costume's `startingItems` at
`OnAllDatabasesReady`. This timing matters: the installed game loads costumes
before items. Registration checks both the item ID and the language-independent
name keys, `Item_SwordShieldGrowth_Enhanced_Name` and `Item_BloodofObrus_Name`.
Each registration is independent: missing/mismatched item data skips only that
artifact and logs a diagnostic; missing/mismatched costume data skips both.
An existing item with the same ID is neither duplicated nor claimed on unload.

The game then handles each item exactly like its other costume starting items:

- `PlayerAvatar.UpdateCostumeData` grants it on the server and records the
  returned instance ID in `costumeStartingItemInstanceIDs`.
- Switching costumes removes the recorded instance via `RemoveStartingItem`,
  including inventory, temporary/pending additions, sub-bag and dropped-item
  cleanup. Switching back creates the costume's starting items again.
- Removal targets each costume-owned instance, not every item with ID 1314 or
  1171. Independently obtained copies are left to normal game rules.
- Run restart clears the run inventory and calls `RestockStartingItem` after
  player initialization. It uses the current costume-owned list.
- Joining players use the native costume initialization and inventory
  synchronization. There is no addon state keyed by a previous connection.

Only the **host** needs the addon for guests to receive the artifacts. Guests
already have the native item definitions, prefabs and effects. The host's costume
preview automatically includes their icons; an unmodified guest's costume-selection
preview still uses its own stock data. Actual inventory and effects are native
networked state.

Saved runs keep their native inventory restoration rules; this is a starting
grant, not a retroactive grant into an already saved run. No saved inventory
is edited and no repeated grants are sent during play.
Native resume rebuilds the costume's starting-item list separately from saved
inventory instance IDs. Normal costume/preset edits are blocked in the dungeon;
the next run restart clears the saved copies before restocking. No additional
ownership guarantee is provided for debug commands or other mods that force a
costume change inside a restored dungeon run.

## Lifecycle and performance

Duplicate database-ready callbacks reuse the existing entries. Other costume
templates and source arrays are preserved. Addon unload restores its template
edits in reverse registration order, retaining later edits from other addons.
Already-created item instances
remain under the game's costume ownership and removal rules.

There is no new per-frame work, player scanning, Harmony patch, custom RPC,
per-player dictionary or reconciliation feature. Registration allocates a small
array per missing artifact and relies on the native grant/removal pipeline thereafter.

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
reconnect an unmodified guest, and verify that independently obtained Crests and
Blood of Obrus survive the costume switch while the starting copies are removed.

## Blood of Obrus follow-up (0.46.0, 2026-10-11)

Re-read installed `resources.assets`: item `1171_BloodofObrus`, ID `1171`,
name key `Item_BloodofObrus_Name`, prefab `DCharm_1171_BloodofObrus` in
`sharedassets0.assets`. Native EN/KO catalogs provide **Blood of Obrus** and
**오브러스의 피**. No addon translation, custom item, balance edit or guest asset
is needed. The installed `Assembly-CSharp.dll` still matches SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.

Implementation extends the existing registration with independent artifact
entries. This keeps a missing Blood of Obrus definition from disabling Iron Wall
and vice versa. Cleanup removes only each owned template addition; actual item
instances continue through the unchanged native lifecycle described above.

The focused fixture now includes both artifacts, either/both pre-existing,
independent invalid definitions and recovery, replaced templates/databases,
in-place edits, duplicate other-addon additions, and reverse-order unload.

Verification for `0.46.0`: all **69** artifact registration/ownership checks
passed, as did the portable and installed-game compatibility suite (including
native costume grant/removal, restart, guest synchronization and preview checks).
Debug and Release builds completed with zero warnings/errors, all with
`-p:DeployMod=false`. No game deployment or live multiplayer test was performed.
