# Wing-Eared Rabbit level-up potion rewards

## Design

An off-by-default host toggle grants one native potion per earned level while
`HolyRabbit` is equipped. Command: `/one rabbit level-up-potion on|off`; include
the setting in the Rabbit panel, status, reset, EN/KO text and preset v10. Reuse
the shared session policy at the moment of use rather than copying it per player.

The user selected all potions other than HP/MP-related types. Cross-check the
[requested index](https://sephiria.world/potions) against installed native item
effects. Uniformly select from native IDs 28,29,30,31,33,34,35,38,39,40,41,42,43,
46,47,48,49,50,51. Exclude HP/MP recovery, final HP, lifesteal and random-stat
potions with possible MP regeneration. Include native hidden non-HP/MP entries;
enchantment/combo rewards retain their ordinary indirect effects.

Wrap only the native earned-level call in `LevelController.LocalAddExp`.
Preserve the XP loop and native level-up behavior; do not hook restored reward
queues or initialization. Capture/recheck avatar, connection, costume, inventory,
run and policy around native callbacks. No polling or persistent player ledger.

Use `GridInventory.LocalAddItem` inside a native permission scope. An explicit
false queues one unit in native `temporaryInventory`, matching the game's own
purchase overflow path. Do not retry after an exception or ambiguous mutation.
Guests need no addon/assets/protocol changes. Keep gameplay-hook compatibility
separate from the existing potion-drinking hook compatibility.

## Steps

- [x] Add settings/command/preset regression tests and implementation.
- [x] Add level-up hook/runtime tests, verified native potion catalog and feature.
- [x] Integrate Entry, independent availability, panel/status and EN/KO tooltip.
- [x] Verify multi-level XP, no restore replay, costume/toggle/session changes,
  reconnects, full inventory, nested native callbacks and failure containment.
- [x] Build Debug/Release with DeployMod=false, run relevant suites and installed
  IL checks, review and document. Publish verified work by conventional commit/push;
  never deploy.

Verification: 94 earned-level reward checks, 834 runtime integration checks,
44 description plus 6 localized description checks, 214 drink-hook plus 12
localized alert checks, 27 starting-artifact checks and 48 localization-loader
checks passed. Portable command/preset/catalog tests and installed-game IL
contracts passed. Debug/Release builds have no warnings/errors. Native inventory
permission is not reentrant; the implementation borrows a caller's existing
scope and the regression test verifies the caller retains it. Live gameplay and
Unity UI verification remain manual.

The prior Sample Survival fix (`53c3fc6`) is committed locally but awaiting push
after repeated GitHub server errors. Preserve that commit and publish it with
this work when the remote recovers; never rewrite or force-push.
