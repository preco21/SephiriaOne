# Requital and Heaven starting Slates

Requital was implemented in 0.72.0 on 2026-10-20; 0.75.0 adds Heaven selection.
The audited native items are **Requital / 보은**, ID **2100**, and
**Heaven / 천국**, ID **2099**. Both are Eternal StoneTablets.

Host controls live in **Items → Starting Slates**, with a dropdown selecting
Requital or Heaven for both automatic and manual grants. Requital remains the
default. Automatic grants default off; count defaults to 1 and accepts 0–20.

| Command | Effect |
| --- | --- |
| `/one slates type requital|heaven|reset` | Select the Slate for automatic/manual grants; type reset selects Requital and retains count/auto. |
| `/one slates auto on|off|reset` | Enable or disable the automatic starting loadout. Reset disables it. |
| `/one slates count N|reset` | Set the number of copies; reset returns to 1. |
| `/one slates give` | Give the configured number of ordinary copies to every current ready player. |
| `/one slates status` | Inspect settings and native feature availability. |
| `/one slates reset` | Restore Requital, automatic off, count 1. |
| `/one save` | Persist host intent in the shared preset. |

Enabling or increasing the automatic loadout in the lobby delivers the extra
copies immediately. Inside a run it registers the additional copies for the
next fresh-run starting loadout. Floor changes, consumption and inventory edits
do not add copies. Reducing the count or disabling the option retracts only
addon-owned automatic instances, including transferred, engraved, pending,
sub-bag and dropped representations. Ordinary native copies and the immediate
Give action's copies remain unaffected.

Changing type retracts the previous automatic loadout's exact IDs and creates
fresh IDs for the newly selected type. As with count increases, lobby replacement
is immediate; in-run additions wait for the next fresh run. Manual **Give now**
can deliver the chosen type immediately during a run without a restart.

Manual Give uses the configured count regardless of the automatic toggle. Its
items are ordinary one-shot gifts: they do not restock or become part of later
automatic retraction. The action reports confirmed copies granted, unready
players and failed players; a failed callback may already have written an item.
Count zero performs no grant.

## Native evidence and implementation

Installed `Assembly-CSharp.dll` SHA256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
The asset audit identifies `resources.assets` path ID **29131**, with resource
prefab `sharedassets0` path ID **67970**. Runtime checks require ID 2100, the
exact name key `Item_StoneTablet_Requital_Name`, type StoneTablet, rarity Eternal, active type WitchHat and a
non-null resource prefab. Checks repeat before actual additions, including
between callback-bearing writes.

Fresh Heaven asset inspection in 0.75.0 confirms `resources.assets` path
**29062**, name `2099_Heaven`, name key `Item_StoneTablet_Heaven_Name`, resource
pointer **file 3 / path 68209**, StoneTablet/Eternal and active type **Hidden**.
`Hidden` is the installed catalog flag, not a reason to substitute another item
or change the global catalog. Native `ItemDatabase.FindItemById` returns the
registered entry and `GridInventory.LocalAddItem` does not filter `activeType`.
The addon checks each chosen type's own identity/prefab independently; an invalid
Heaven entry does not disable Requital, or vice versa.

The fresh asset file SHA256 is
`906ba704cf4eefa8e300501c3b4aeefdeae91883885eb8fbe31ab754c60a3f9b`.
The exact Heaven object's raw SHA256 is
`54301d6954b90528d0700175247413ca74d5ad27a13f52529fba27a371eac0a8`.
The managed identity was reconfirmed through REA; the resource fields were read
with the existing local UnityPy tooling. No assets or decompiled source are
distributed with the addon.

`RequitalRuntime` uses a separate optional
`ReconciliationCoordinator<FriendlyFireParticipants.Receipt>`. It observes
immutable settings, native inventory identity and catalog revision. Readiness
belongs inside the first apply attempt; loading cannot reset an applied or
faulted cursor and replay an addition. Stable frames do not enumerate item
stores. The independent coordinator cannot block critical stats/Fountain work.

Automatic IDs come from `ItemDatabase.GenerateInstanceID`. The runtime records
the exact ID **and entity type** before any native callback and registers `ItemMetadata` in native
`GridInventory.startingItems`; it never uses the costume-owned private ID list.
This avoids inferring provenance from newly seen inventory entries, item type,
network ID or another addon's simultaneous grant.

Lobby delivery follows the audited native `AddStartingItem` operations:
`LocalAddItem`, then `DungeonManager.BoundOnServer` and
`OwnRestrictionOnServer(StartingItem)`. A definite full-inventory result queues
the exact `GridInventory.StartingItemAdditionFailedInfo` in native
`additionFailedStartingItems`. Existing write permission is borrowed because
`GridInventory.Permission` is non-reentrant.

The guarded `RestockStartingItem` prefix adjusts registration metadata before
the original fresh-run delivery. Its verified native caller is
`PlayerSpawner.RestartNewGame(int)`. Native restock's `AddItem` normally ignores
the bool returned by `LocalAddItem`, losing full-inventory additions. A guarded
transpiler replaces that one call with an adapter: ordinary IDs retain the
original `AddItem`, while proven automatic Requital IDs use native placement
and the native pending queue on a definite failure. The original restock still
owns iteration and applies its normal binding/restrictions. It delivers once.

`GridInventory.ForceRemoveAll` does **not** clear
`additionFailedStartingItems`. Native `Update` can drain that queue during the
restart coroutine's three 0.1-second waits before `RestartNewGame`. Before
restocking a proven automatic ID, the adapter removes only that ID's stale
queue entries, then checks connected carried, engraved, fixed-engraved,
sub-bag and temporary stores for an already delivered instance. It skips an
existing ID; otherwise it places it or queues exactly one retry. Other native
pending entries retain their original behavior.

Manual Give uses `LocalAddItem` directly with ordinary IDs and no starting-item
registration, binding or restriction. A definite failure enters native
`temporaryInventory`. Exceptions never cause an overflow copy or automatic
retry, because a callback may already have committed the item.

## Ownership, persistence and failure behavior

Native `globalItemStatTable` stores an exact-ID marker whose value is the
original owner's `playerGuid`. The native run-save path persists this table.
Saved-run/reconnect enrollment recovers only those exact marked IDs and rebuilds
missing starting registrations without delivering another live copy.

The historical internal `RequitalRuntime`/`RequitalSettings` names and owner /
retirement marker names remain to preserve existing ownership. New automatic
Heaven instances additionally store `SephiriaOne_StartingSlateKind = heaven`.
Absence of that marker means a legacy Requital instance. Never infer the old
item's type from the current selector: a selection change must retire the old
instance rather than reinterpret its ID. Weak receipts retain full metadata
when native fresh-run clearing erases the global marker table. Unknown saved
type markers pause additions; off/zero cleanup can still retire exact IDs.
Preset v40 records `slates type 1` for Heaven. Earlier versions and omitted
selection retain Requital. Commands use names, not numeric item IDs.

Weak inventory/dungeon/owner-scoped receipts bridge native fresh-run clearing
of the global table while `startingItems` survives. Restock republishes the
markers. Scope resets clear reconciliation cursors, not this weak provenance.
Different native inventories, dungeons and owner GUIDs cannot reuse a receipt.

Retraction uses native `RemoveStartingItem(id)`, which covers inventory,
engravings, fixed engravings, overflow, pending starting additions, sub-bags,
related conversions and ground drops. Current connected inventories are
considered independently of the original owner's presence. Retired exact IDs
leave saved tombstones in the native global table, so a transferred copy saved
by an offline recipient is removed when that recipient returns. Tombstones
survive until native fresh-run metadata clearing. No custom guest protocol,
asset, client addon or guest-side settings are required.

Automatic callback faults latch against further additions. Settings changes,
loading and database readiness do not automatically retry an ambiguous write.
An explicit off/count-zero operation can remove proven IDs; after verified
complete cleanup a later enable can start again. Shutdown retracts automatic
items and stops hooks only after cleanup succeeds. Cleanup failure propagates
to the existing Entry unload recovery flow, leaving the hooks and receipts
available for a safe exact-ID retry.

All writes recheck authority, owner, inventory, run/generation and current
intent around native callbacks; shared connection receipts additionally guard
ordinary reconciliation and manual batches. The fresh native restock boundary
can occur while shared participant initialization is temporarily retired, so
it validates the native owner directly and uses a current shared receipt when
one is available.

Both kinds use the same coordinator and native hooks; there is no second grant
loop. If a native callback changes settings during an automatic write, invalidate
the current observation explicitly so the coordinator's post-apply capture
cannot swallow the new intent. This permits old-ID cleanup while the fault latch
continues to block any replay of an ambiguous addition. Manual batches capture
the full settings value and stop on a mid-callback type/count/toggle change.

## Verification and remaining live checks

The isolated `tests/SephiriaOne.RequitalTests` executable links the production
runtime, settings, coordinator and Harmony hook code. It covers:

- Lobby grants, repeated frames, floor changes, fresh runs and full inventory.
- Pending queues retained across native clearing, pending delivery during
  restart delays, and failure on the first item in a multi-item restock.
- Native originals, manual ordinary gifts, transferred engravings, offline
  recipients, disconnected owners, saved markers and reconnect reconstruction.
- Readiness delays, native callback failures, owner/database replacement,
  exact ownership when another callback adds a native Requital, explicit fault
  cleanup, unloading and reloading the same native inventory.
- Zero allocations and no extra delivery over 10,000 stable ticks with nine
  players.

The main compatibility suite separately validates the installed native
inventory, pending-list, restock, save, exact-ID cleanup, Slate replication,
transpiler and Entry lifecycle contracts. These are fixture/native-code checks,
not proof of live host/unmodified-guest gameplay. Live checks still need lobby
and second/third-run delivery, engraving effects, full-inventory recovery,
save/rejoin after transfers, and host/guest UI rendering.

Builds and tests use `-p:DeployMod=false`. No game deployment was performed.

Final verification on 2026-10-20: Release build has zero warnings/errors;
45 inventory runtime regressions, 392 Slate policy/preset checks, 3,552 shared
runtime checks, 42 Entry lifecycle checks and the installed native compatibility
suite passed. Combat (6,188 checks), potion hooks (301 plus 12 localized alerts)
and panel primitives (28 checks) also passed during this change. Review caught
and corrected pending-delivery replay across restart and missing fault admission
in the restock adapter. No live multiplayer session was launched.

Heaven-selection verification (0.75.0, 2026-10-20): Release build passed with
zero warnings/errors; 67 inventory assertions, 417 policy/preset checks,
3,610 shared runtime checks, 43 Entry lifecycle checks, 28 panel checks,
50 localization checks and 4,153 bundled catalog checks passed. The installed
native suite verifies both identity constants, the shared dropdown command,
no-notify refresh, direct insertion, restock and guest Slate serialization.
New cases cover type changes in lobby/run/restart, retained manual gifts,
pending overflow, saved/rejoined Heaven IDs, offline owners/recipients,
independent catalog failure, and callback changes. Both Requital and Heaven
retain zero allocated bytes across 10,000 warmed stable nine-player ticks.
Review corrected the selector's initial footer overlap; it now shares the
heading row and keeps the original page bounds. No deployment or live guest
rendering/gameplay validation was performed.
