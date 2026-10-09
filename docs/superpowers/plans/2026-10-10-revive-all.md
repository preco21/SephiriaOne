# Revive-all recovery implementation plan

**Goal:** Host command `/one reviveall` and Combat-tab button restore dead session players through native revival at full HP, with stock guests.

**Design:** One event-time service shared by command and button. Gather exact current dead avatars, validate finite positive native MaxHp/readiness, call `Revive(MaxHp)` once per candidate, recheck session/player identity around callbacks, and report success/partial failures. No per-frame scans, score resets, living-player healing or saved option. It remains usable with friendly fire off or unrelated reconciliation faults.

**Native findings:** `Revive` restores death/HP SyncVars, native events, remote inventory and brief invulnerability; its stock RPC restores rendering and camera via OnReviveClientside. `PlayerSpawner.HandleDieServerside` immediately emits game over when all players are dead. Game-over UI disables/deletes the current run save and settles quests, so recovery after settlement is unsafe. Skip that one callback only for the exact admitted friendly-fire player death; enemy/environment deaths retain native behavior. Revive-all rejects disabled run saves, victory and leave/restart settlement.

**Alternatives:** Writing HP/IsDead alone misses events and guest camera state. Reconstructing a settled run would require save/UI/quest rollback on unmodified guests. Use native revival and prevent destructive settlement at the friendly-fire death boundary instead.

**Review follow-up:** Native revival clears IsDead before callbacks and sends its RPC afterward; catching the entire call strands partially restored avatars. An independently validated two-call transpiler wraps only HP/revival event dispatch for the exact action-owned avatar, continues later subscribers/native tail after logged errors, and aborts if scope changes. Ordinary/nested unrelated revival is unchanged. If recovery hooks are unavailable, reject the command before mutation and retain normal game over.

## Tasks

- [x] Add failing runtime command/revival and executable death-boundary regressions.
- [x] Add `Features/Combat/ReviveAllAction.cs`; connect SettingsActions and Combat button, EN/KO labels/help.
- [x] Add exact friendly-death game-over guard to shared combat hooks with installed-game validation.
- [x] Test host/dead-host, off, nobody dead, invalid/loading/departed players, partial failure, scope change, repeat action, new runs and native guest revival contract. Update docs/version, review and non-deploying builds.

Verification: 1,306 runtime checks, 259 executable combat checks, 69 Bat lifecycle
checks and the full portable/catalog/installed-game suite pass; Debug and Release
builds report zero warnings/errors. Delivery follows the repository's authorized
Conventional Commit and push workflow on `main`.

All builds/tests use `-p:DeployMod=false`; no deployment or game launch. Already settled runs cannot be safely resumed by this action and are reported explicitly.
