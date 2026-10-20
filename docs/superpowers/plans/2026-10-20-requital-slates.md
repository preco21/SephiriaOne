# Requital Slate grants

Goal: host-configurable extra Requital (보은, native ID 2100) starting Slates,
plus an immediate one-shot grant to all current players. User clarified the item
and starting-loadout cadence: lobby/fresh run, never additional copies per floor.

Design: immutable settings (default automatic off, count 1, range 0..20), shared
host command/snapshot/preset flow, Items-tab controls. Automatic grants register
native starting items with exact-ID addon provenance. Reducing the count or
disabling the option retracts only those automatic instances, including native
bag/engraving/ground representations and transfers. Manual grants are ordinary
one-shot items, do not restock and are not retracted by automatic settings.

Use the existing reconciliation coordinator with a separate optional inventory
rule; errors must not block stats/Fountain. Observe settings and native lifetime
boundaries, not inventory quantity on every frame. Native RestockStartingItem
remains the fresh-run delivery owner. Rejoins recover provenance from saved native
metadata; do not infer ownership from item type or stale connection IDs.

- [x] Test parser/default/reset/preset and native grant/retraction scenarios.
- [x] Add focused Requital settings, native grant adapter, reconciliation and
  guarded restock boundary under Features/Items/Requital/.
- [x] Wire shared policy, command/button, snapshot, lifecycle, EN/KO and v38 preset.
- [x] Verify first/repeated runs, live settings, native originals, moved/engraved
  copies, full inventories, callback failures, reconnects, no stable-frame scans.
- [x] Check installed native inventory/serialization contracts, review, update
  docs and prepare publication. Every build uses DeployMod=false; never deploy.

Native evidence: Assembly-CSharp SHA256
c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510;
REA parent ev_ab49f229cac337542482edb4609b8311559c09815ea01ce78b05dafb117775e7.
resources.assets path 29131 is Item_StoneTablet_Requital_Name, Eternal rarity,
WitchHat active type (not Disabled), resource prefab sharedassets0 path 67970.
GridInventory.AddStartingItem suppresses live delivery inside a run, so the
manual action must use LocalAddItem with the existing native write permission
and definite-full overflow handling. RemoveStartingItem owns exact-ID cleanup.

Completed implementation: 0.72.0. Final evidence and live-test limits are in
[Requital Slates](../../requital-slates.md). The separate user-requested infinite
PvP HP-potion default is now on, with explicit saved opt-outs retained; its focused
commit is `ccbe810`. No deployment is authorized or performed.
