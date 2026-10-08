# Given-item restriction toggle

Added in `0.30.0`. Open `/one ui` → **Items**, or use:

```text
/one items unlock on
/one items unlock off
/one items status
/one items reset
/one save
```

The toggle defaults off and affects all players in the current hosted session,
including unmodified guests. Save explicitly to retain it in future sessions.
Off/reset restores recorded restrictions but does not recall sold/transferred
items or reverse money transactions.

## Installed-game findings and limits

- Costume/passive starting grants use `GridInventory.AddStartingItem`, including
  deferred full-inventory retries and `RestockStartingItem`. They set `Bound`
  plus `OwnRestriction=StartingItem` (1) on each instance. The hard-mode starting
  grant uses the same flags.
- `PlayerSpawner.AddDimensionPocketItemsOnServer` (Wishing Fountain carryover)
  sets only `Bound`. Its items can already be sold; their owner-only dropped
  pickup is the restriction this option removes.
- All three stock `UI_ShopPanel` sale paths read synchronized `OwnRestriction`.
  The same flag controls Fountain storage and result recovery, so those also
  unlock. No host-only UI patch would provide this behavior to unmodified guests.
- Native drop paths read `Bound` to choose the spawned Item's `isBound` SyncVar
  and client authority. Existing drops need both updated: retaining guest
  authority after unlocking would destroy the drop on that guest's disconnect.
- Intrinsic `ItemEntity.cannotThrow`, destruction rules, merchant funds, trade
  modes and multiplayer-zone restrictions remain native. In particular, the
  curse tablet (12000) granted to Red Monster Rabbit/Skeleton has a local
  `cannotThrow` property. Removing that on stock guests requires editing their
  item definition; this host-only option cannot do so.
- Asset inspection covered all 395 native ItemEntity and 28 CostumeEntity assets
  in `resources.assets`, checking the full serialized field lengths. Costume
  starting entries are Charms or StoneTablets (native stack limit 1), including
  the no-drop curse noted above. Fountain items are Charms. The potential split
  stack problem, where extra dropped units receive new IDs without binding
  metadata, does not apply to these native grants. Custom addon stackable grants
  are outside this compatibility guarantee.

The overlay leaves grant/restock/costume cleanup in the native game. Completed
transfers are not retroactively undone on reset. The usual costume-owned copy
still follows native removal; this option does not manufacture replacement items.

## Design and implementation plan

Default off; host controls `/one items unlock on|off`, `status` and `reset`.
Use the shared command, session policy, preset and panel infrastructure. Store
this option in preset v11 only when enabled; older presets retain native behavior.

The installed game uses `OwnRestriction=1` for starting/given items, and `Bound`
for the original owner. Fountain grants set only `Bound`; sales already work.
All native binding producers are starting grants (including the hard-mode grant)
and Fountain grants. Remove these instance restrictions through native synchronized
metadata, so unmodified guests see the same state. Do not edit item definitions,
intrinsic `cannotThrow`/destroy-on-discard rules, merchant funds, or trade zones.
The starting-item flag also controls Fountain storage and result recovery; its
removal necessarily unlocks these surfaces on stock guests as well.

Use an event-driven overlay with originals retained per key. Existing items are
processed on toggle/scope attachment; new grants, native rebindings, resets and
saved-run loads arrive through the dictionary callback. Native unbinding/removal
must retire the corresponding original rather than resurrect it on reset.
Ground-item binding SyncVars and authority must match the effective metadata.
No per-frame item scan or new network protocol. New/rejoining guests receive the
game's full current dictionary and ground-item state.

Native saves must serialize original restrictions without changing live metadata
or emitting temporary packets. Reset/unload restores recorded restrictions;
completed trades and sales are not reversed, and costume grant/restock/removal
remains the native lifecycle.

- [x] Add failing command/preset and overlay lifecycle coverage.
- [x] Implement the metadata overlay, native hooks and compatibility checks.
- [x] Integrate shared session settings, Items panel and EN/KO text.
- [x] Verify existing features, native contracts, saves, ground items, repeated
      re-entry and failure recovery. Build with `DeployMod=false`.
- [x] Review and document results; commit and push without deploying after checks.

## Verification

Verified on 2026-10-09: Debug/Release builds have zero warnings/errors; 850 shared
runtime checks, 26 native-hook/lifecycle fixtures, 28 item policy/overlay checks,
48 localization checks, and the complete portable/installed-game suite pass.
The catalog suite includes 1,146 bundled translation checks.

Automated checks exercise default/off/reset, per-instance originals, repeated
commands, new grants, native rebinding/unbinding, foreign metadata, scope changes,
saved-run reconstruction, second-run clears, guest command rejection and saved
presets. Native-hook fixtures check existing/new ground drops, current rejoined
owner identity, disconnect-safe authority, saves without live writes, after-write
failures, authority-restoration retries and unload. Shared command fixtures cover
failed enable/off and failed-preset reset recovery through the normal write journal.
Installed assembly checks verify stock guest UI readers, grant/drop/save contracts
and native binding setters. Independent review found and fixed stale ground
authority and recovery-token bugs. No live multiplayer or panel-rendering test was
performed, and no deployment was run.

Manual checks after installation:

1. With the option off, verify native given-item sales and owner-bound pickup.
2. Drop a costume artifact and Fountain artifact before enabling. Enable as host;
   an unmodified guest should be able to pick them up, and given-item sales should
   work through click, drag and temporary inventory. Normal shop funds still apply.
3. Grant or select a new costume/Fountain item with the option on. Check drops,
   sales and sharing; verify ordinary loot and intrinsic curse tablets keep their
   native behavior.
4. Leave an unlocked artifact on the ground, disconnect its original guest and
   rejoin. The drop must survive and remain shareable. Turn the option off and
   confirm the retained native ownership/sale restrictions return.
5. Save a run, restart with the option off (or without the addon), and check native
   restrictions. Repeat with a saved enabled preset, then reset. Return to lobby
   and begin a second run; no previous instance's original owner may leak forward.
6. Switch away from the granting costume and check normal cleanup/restocking.
   Off/reset must not reverse completed trades or issue duplicate grants.
