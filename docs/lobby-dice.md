# Dice changes in an existing lobby

Implemented in 0.86.1, 2026-10-22. Supersedes the original future-start-only dice
behavior in the early resource design notes. Leaves keep their existing separate
first-departure grant.

## Cause and behavior

Dice policy was read only at `PlayerSpawner.Initialize`. The Resources command
validated and saved the setting but skipped writes for both starting-only
resources. Ready-player inheritance and maintenance also skipped dice, so a
setting changed after the lobby was created could not update that lobby.

The shared command, inheritance and maintenance paths now reconcile dice while
the run has not started. Set, offset, multiplier and reset all use the same
native-relative planner and journal. A saved preset loaded into an existing
lobby uses this path too; no additional polling loop or guest addon is needed.

For each player:

`new balance = old balance + new allowance - old maximum`

For example, native maximum 3 and current balance 1 become maximum 9 and balance
7 after `dice x3`. Repeating x3 keeps 7. Reset returns maximum 3 and balance 1.
Native earned dice above the maximum are also preserved. A decrease rejects if
it would make the balance negative, before any player's values are written.
Clamping that decrease to zero would forgive spending and let repeated toggles
create dice, so the implementation deliberately rejects it.

After first departure, commands still store future fresh-run intent without
changing current dice. Eligibility checks the live `isRunStarted`, persisted
`RunStarted`, and native `IsInDungeon` flags, so a later town visit is not mistaken
for a new lobby. A departure during a planned write invalidates it.

## Synchronization and lifetime

- Native `NetworkmaxRerollDice` and `NetworkrerollDice` SyncVars update maximum
  and balance. The latter invokes the native client dice-change hook.
- Ownership excludes the addon's own contribution from xN baselines. Maintenance
  observes maximum/ownership and policy, never current spendable dice, so spending
  does not trigger refills. Absolute Set retains subsequent native maximum gains.
- The existing starting-grant token, GUID/save-slot checkpoint and native saved
  balance are updated with the live change. Duplicate initialization cannot
  overwrite it. Disconnect/reconnect restores spent balances, then ready-player
  inheritance applies only a changed pre-run allowance.
- Shared writes are absolute values, guarded by player/session identity. A
  durable pending marker covers native and checkpoint writes. Partial results
  cannot be replayed as delta grants; same-identity explicit recovery finishes
  the recorded values. Changed run/identity or intervening unrelated writes
  refuse recovery. Unload keeps the existing spent-balance behavior.
- Already-created lobby offers are not regenerated. This fixes the granted dice
  state and native counter, not procedural offer generation.

## Native evidence and verification

Inspected installed `Assembly-CSharp.dll` (managed artifact identity): SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA evidence `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`
establishes static artifact identity, not live guest verification.

Matching local decompilation shows the dice restore in `PlayerSpawner.Initialize`,
the generated native SyncVar setters and `HookRerollDiceValueChanged`, and the
live/saved run-start flags in `DungeonManager.LoadStageAndMove`. No new native
patch boundary is introduced. Proprietary decompilation remains outside git.

Regression coverage includes an initially failing late-command test, native-hook
fixtures for grants before/after initialization, spent/earned balances, saved zero,
reconnect, unload, native-write failures/recovery and departure races. Shared
runtime tests cover per-character xN, ready-state enrollment, preset reload,
native gains, all-player decrease validation and mid-run isolation. Installed
assembly tests verify the native grant rewrite and SyncVars. Live host/guest UI
and reconnect testing remains necessary; builds are explicitly non-deploying.
