# Friendly-fire player protection and companion attacks

## Evidence and scope

The user reports that sword-and-shield guarding spends MP when another player
attacks, but unguarded HP does not decrease. Installed `UnitAvatar.ApplyDamage`
invokes the attacking player's `OnAttackUnitBeforeOperation` callback before
guard checks. `PlayerAvatar.HandleBeforeAttack` marks highly friendly targets as
denied. The method checks that failure after guard processing; guard feedback and
MP use can therefore occur even though ordinary damage is later rejected.

The existing patch handles initial faction/leader checks only. Its executable
fixture omitted the player's before-attack callback. Adding that callback and
its native failure ordering reproduces the bug: the existing 50% HP-damage test
fails with production hooks installed.

Bypass only this native protection callback when the existing host-authorized
friendly-fire context matches the exact attacker, victim and damage instance,
and the victim is a player or player-led follower. Preserve NPC safe mode and
Shield of Reason/crime handling. Keep other attack callbacks and their failures,
guard/parry/invulnerability/evasion, shields, MP shields, scaling and death native.
Do not clear `damage.failed` globally or change factions/DamageInstance fields.

## Work

- [x] Inspect native ordering, existing hooks, executable fixtures and recent log.
- [x] Reproduce the missing callback in a failing executable regression.
- [x] Patch the exact callback with compatibility checks and scoped matching.
- [x] Cover guarded and unguarded hits, shield overflow, team followers, safe NPCs,
  off/reset, self/system/enemy paths, other vetoes and nested-context isolation.
- [x] Verify installed callback/event registration and native HP replication;
  reject changed native contracts. Run focused and shared regression suites.
- [x] Document the correction and verification limits, increment version,
  run Debug/Release builds with deployment disabled and obtain independent review.

No AddOns deployment. No custom guest state, new polling or per-hit allocations.
Live multiplayer testing is not claimed by native inspection or fixture tests.

## Confirmed companion scope

The user confirmed companions should actively attack other players while friendly
fire is enabled, but never their own owner. Use a host-only postfix on
`UnitAI_NewBasic.GetRelation`: player-led companions treat other players as hostile.
This is shared by Collin (`UnitAI_WeaselKnight`) and the standard native AI family.
Native sight, target availability, peaceful-area checks, movement and attacks stay
in charge. A faction-wide change would also affect ordinary NPCs and healing;
manually assigning targets would duplicate native scheduling and range rules.

At hit time, resolve the current direct `NetworkLeader` and apply the existing
friendly-fire scale to companion hits against other players. Explicitly block hits
against the companion's owner. Do not scale companion attacks against monsters or
broaden their attacks to unrelated NPCs. Keep the actual source separate from the
player credited in kill notices. No cached connection IDs, ownership, targets or
per-companion policy; off/reset and ownership changes use the next native query.
At 0% allied damage companions retain normal targeting and allied hits are blocked.

- [x] Add executable AI/companion-hit regressions and observe missing behavior.
- [x] Add shared AI hook and owner-aware hit scope with native compatibility guards.
- [x] Cover off/zero/guest mode, owner exclusion, ownership changes/rejoins,
  enemy damage, shield/HP scaling, nested hits, kill attribution and unload.
- [x] Verify native AI consumers, Collin attack path and normal target guards.
- [x] Update English/Korean panel text and feature docs; version 0.37.0.

## Off-transition follow-up and verification

The user explicitly required stopping companion attacks when the toggle changes.
Independent review found that native archer follow handlers do not release held
weapon triggers. The first relation-only implementation therefore prevented
damage, but did not stop every ongoing attack. A stateful archer regression failed
on off-transition. The final prefix uses native `isInBattleActiveByAI` and calls
`SetTarget(null)` for a no-longer-hostile player target, triggering virtual
`OnLostTarget` cleanup. The native flag covers retaliation before the first search,
when the native previous-relation field has not been updated. Normal friendly
targets are not repeatedly cleared, and attacks against monsters are not canceled.

Verified on 2026-10-09: 101 executable combat checks; 1,250 shared runtime and 69
Bat lifecycle checks; 48 localization checks; full portable and installed-game
compatibility suite including 1,449 catalog checks. Debug and Release builds passed
with zero warnings/errors, using `DeployMod=false`. Independent review's held-trigger
finding was fixed and rechecked; no remaining actionable findings. Live gameplay
and guest rendering remain untested. Repository workflow commits and pushes these
changes without deployment.
