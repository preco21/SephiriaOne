# Costume-bound Collin starting artifact

User-approved scope: one default-off option grants native Hiring Crest “Collin”
to Mole, Farmer Squirrel and Turtle as a costume-bound starting artifact.

## Design

Use item 1197 (`Item_WeaselKnight_Name`) and costume IDs `Mole`, `Squirrel`,
`Turtle`. No custom follower, asset, RPC or guest addon is needed. Native
inventory replication and the item's existing companion effect do the work.

Read the shared session policy at costume initialization and fresh-run restock,
including before the next synchronization frame. A setting change takes effect
at the next costume equip or fresh-run restock; it does not inject artifacts into
an ongoing run. Native saved inventories resume unchanged. Register exact item
IDs in the game's costume ownership list so switching costumes removes only the
grant, preserving independently obtained Collin copies. Record addon provenance
in native global item metadata so reloads do not lose identification. Remove
transferred grant instances from connected players when their source costume
changes. Never refresh all costume stats/items just to grant this artifact.

No frame polling or cached per-player policy. Weak avatar/dungeon receipts retain
exact grant provenance across the native restart metadata clear. Normal inventory-full
fallback, companion creation, item restrictions and restocking stay native.
Compatibility failures disable new grants and leave native gameplay running.
The host costume preview remains native; the mod panel explains the extra grant.

## Implementation and verification

- [x] Add failing tests for default/off, eligible costumes, duplicate prevention,
  switching, independent copies, restock, current policy, reload and transfer.
- [x] Implement focused `Features/Collin` native lifecycle hooks and identity guards.
- [x] Add `/one collin on|off|status|reset`, shared policy, snapshot and preset v15.
- [x] Group Bat and Collin controls under Costumes; translate all new text EN/KO.
- [x] Verify installed native IL contracts and fixture lifecycle/command tests.
- [x] Run Debug/Release builds, runtime, portable/native and localization suites
  with `-p:DeployMod=false`; independent review, focused fixes and re-verification.
- [x] Update history/usage docs.
- Delivery: conventional commit and push main after verified final diff. No deployment.

Native saved-run inventory restoration reconstructs costume starting-item IDs,
as it does for vanilla starting artifacts. In-dungeon costume editing is blocked
by the game. Forced costume changes by another addon/debug tool during restored
runs are outside that native guarantee; never identify/remove unmarked Collin
items by entity ID alone.

## Verification result

Debug and Release builds passed without warnings. Collin lifecycle fixtures: 29;
Collin policy/preset checks: 23; shared runtime: 963; bundled catalog: 1311;
localization: 48; existing Rabbit artifact fixtures: 27. Installed-game IL
contracts passed. Five-player fixture synchronization remained at 0 allocated
bytes/tick. Independent review identified the native metadata-clear edge case;
the failing regression was fixed with weak scoped ownership receipts and passed
on re-review. No live multiplayer or rendered UI test was performed.
