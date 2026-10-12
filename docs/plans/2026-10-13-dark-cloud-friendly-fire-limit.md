# Dark Cloud friendly-fire strike limit

Request: slightly restrain Dark Cloud against hostile players only while friendly
fire is enabled, preserving the artifact's normal behavior. Baseline `888e9bb`.

## Native findings / design

The installed `ComboEffect_DarkCloud.UseCloudCoroutine` repeats multishots with a
0.133-second wait. Automatic discharges, extra discharges, and targeted staff
attacks all reach `FireLightning(UnitAvatar, int)`. The player-combo branch calls
`ApplyDamage`, then `OnAttackUnit` (Blazing Stormcloud uses it to apply burn), then
`TargetCreateFx`. A damage-only throttle would leave the debuff callback active.

Use a prefix on that exact method: at most one strike attempt every 0.5 seconds
per player attacker/victim pair. Reserve before invoking native damage to bound
same-pair reentrancy. Skip the whole repeated strike, including its callback and
FX; keep the caller's native cloud costs/callbacks and random target selection.
Use scaled game time so pausing does not refill the cooldown. Different players,
enemies, non-player combos, and disabled/zero friendly fire keep existing paths.

Reuse live friendly-fire eligibility, participant receipts and combat epochs.
Weak receipt-keyed tables prevent retention of departed players. New connection,
run, selection/match epoch, KDA reset and changed floor identities discard old
cooldowns. Keep the limit host-side: stock damage, debuff and visual replication
remain native. No asset edits, native timer changes, UI option or preset schema.

## Implementation / verification

1. Extend the executable cloud fixture to match the native strike signature and
   callback/FX order. Add a rapid-repeat test and see it fail with existing hooks.
2. Add `FriendlyFireDarkCloud.cs` alongside the shared combat adapters, and install
   its prefix from `FriendlyFireArtifactHooks` after validating signature, owner,
   player-combo, damage, callback and FX anchors. Clear tables on addon unload.
3. Cover exact interval boundary, independent attacker/target pairs, ordinary
   enemies, off/zero, clients, selection/Tournament gates, recursion, exceptions,
   floor/connection/epoch changes, pooling and warmed allocation behavior.
4. Extend installed-game compatibility tests with the actual native entry paths,
   prefix wiring and rejection of changed anchors. Build Debug/Release with
   `-p:DeployMod=false`; run combat, portable/native contracts, session runtime,
   Tournament and performance fixtures.
5. Independent review; document evidence and remaining live balance/guest checks;
   bump patch version, conventional commit and push. Never deploy or launch game.

The 0.5-second value is a conservative starting limit, pending live balance
feedback; it does not cap follow-on damage-over-time ticks from an admitted bolt.

Review follow-up: native multishot coroutines may outlive owner death. A dead
owner bypass reproduced unrestricted queued strikes in the executable fixture;
removing that bypass preserves the limit while allowing native queued strikes
again at the next interval. The native contract also requires resolving inherited
owner/player-combo members from `ComboEffectBase`, matching IL member identity.
Current behavior and remaining live checks are recorded in the
[artifact guide](../friendly-fire-artifacts.md#dark-cloud-player-strike-limit-0512-2026-10-13).

Completed in `0.51.2`: Debug/Release builds and combat, runtime, Tournament,
portable/native compatibility and performance checks passed. Independent review
found the queued-after-death bypass above; its regression test failed before
the correction and passed afterward. No deployment or live game test performed.
