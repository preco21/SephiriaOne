# Wing-Eared Rabbit starting artifact

## Design

Add the existing Crest of the Iron Wall (item 1314,
`Item_SwordShieldGrowth_Enhanced_Name`) to `HolyRabbit.startingItems`. Preserve
the costume's other starting items. The host's native `UpdateCostumeData` grants
and tracks the item by instance ID, removes its own costume items on a switch,
and replicates inventory through the existing game protocol. Ordinary copies of
the same artifact are not removed by entity ID.

Apply after `HorayModAPI.OnAllDatabasesReady`: `GameDataLoader.Awake` initializes
costumes before items. Do not add player polling, custom guest state, independent
inventory grants, or a toggle. The native costume panel can show the starting
item on the host; unmodified guests retain their native costume preview.

## Implementation and verification

- [x] Add focused tests for registration, duplicate callbacks, existing items,
  missing/mismatched data, database reload and restoring only our template edit.
- [x] Implement the database edit in `Features/Rabbit` and connect Entry's
  database-ready/unload lifecycle. Validate both item ID and localization key.
- [x] Verify installed native code still grants only on the server, tracks
  costume item instance IDs, removes those IDs on costume change, restocks after
  restart, and supplies native starting-item UI/network state.
- [x] Build Debug/Release with `DeployMod=false`, run focused and existing
  regression/compatibility checks, review the changes, and record limitations.
- [x] Bump to 0.27.0 and document behavior. Delivery follows the repository's
  automatic Conventional Commit and push workflow. Do not deploy or modify game files.

Independent review found no actionable defects. Saved-run ownership remains
native: inventory IDs are restored separately, ordinary costume edits are blocked
in the dungeon, and restart clears inventory before restocking. Contract checks
cover those boundaries. All verification is offline; no live game was launched.
