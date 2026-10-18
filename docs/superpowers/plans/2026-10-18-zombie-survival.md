# Zombie Survival Implementation Plan

> **For agentic workers:** Use inline execution with verification at each task boundary.

**Goal:** Add host-controlled Zombie Survival on the existing match infrastructure.

**Architecture:** A pure infection/role model supplies a shared-runtime partial.
All damage and targeting use the existing match pair policy; role modifiers share
Tag's impact-time value/settings and damage-budget accounting.

**Tech Stack:** C#, Unity/Mirror native APIs, Harmony, existing executable fixtures.

## Global constraints

- Host-only installation with stock guests; no new packets/assets.
- One-second respawn. Only Zombie kills infect by default; any-death toggle optional.
- Unconverted Humans count as survivors at timeout even during respawn.
- Preserve native PvE, existing modes, K/D/A and participant-selection semantics.
- Never deploy. Every dotnet invocation includes `-p:DeployMod=false`.
- Settings persist, active match/roles never persist. English/Korean parity.

## Task 1: Shared runtime and team policy

Files: `Features/Combat/ZombieSurvival/ZombieSurvivalRound.cs`,
`CombatMatchZombie.cs`, `CombatMatchRuntime.cs`, `CombatMatchTournament.cs`,
`CombatMatchTag.cs`, `FriendlyFireNames.cs`; runtime tests.

- [x] Add an executable failing command/start regression, run the runtime runner.
- [x] Implement explicit Human, pending-conversion and Zombie state; pure methods
  `Infect(int)`, `Revived(int)`, `IsZombie(int)`, `AllowsPair(int,int)`.
- [x] Capture connection receipts; select initial Zombie after warmup. Reuse
  `ConfirmedKill`, successful `Revived` and `Died` recovery. Conclude on Tick
  after native death/attack callbacks, including nested reflection.
- [x] Add mode dispatch for one-second respawns, effective settings, team policy,
  cancellation and role-labelled all-player results before state is cleared.
- [x] Add lifecycle tests (including deadline and callback invalidation) and run:
  `dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false`.

## Task 2: Reusable modifiers and persisted controls

Files: shared `CombatRoleSettings`, `CombatMatchRoles`, `FriendlyFireRuntime`,
`FriendlyFireBalanceBudget`, Zombie command/settings/session files, `SessionPolicy`,
`SessionPreset`, `SettingsActions`; portable and combat fixture tests.

- [x] Generalize the existing chaser value type and revision; retain exact Tag
  defaults, parser precision/ranges, refund math and conservative refill rule.
- [x] Route victim incoming/source refill modifiers through current active role.
  Zombie state lives only in the match; default modifier values are x2/75%.
- [x] Add `/one zombie` command validation, host checks and live setting edits.
- [x] Persist only nondefault settings in preset v31, retain old-header parsing,
  reject duplicate/invalid/new commands in older headers and serialize atomically.
- [x] Verify independent settings, off/expiry/role-loss fallback and Tag regressions:
  `dotnet run --project tests/SephiriaOne.FriendlyFireTests -c Release -p:DeployMod=false`.

## Task 3: UI, localization and completion

Files: `UI/SettingsPanelZombie.cs`, panel dispatch/layout, EN/KO catalogs,
metadata/project version, `docs/zombie-survival.md`, index/domain findings.

- [x] Add compact tab with start/stop, duration/reset, KDA reset, any-death toggle,
  independent modifier toggles/values and existing participants editor.
- [x] Keep panel dimensions, tab readability, draft resets and status current.
- [x] Add EN/KO command/result/countdown/role/error strings and catalog tests.
- [x] Run release build, runtime/combat/portable/native compatibility checks;
  record live-test limits.
- [x] Inspect staged diff and complete the authorized conventional commit/push workflow.

Validation assertions include `AllowsPair(zombie,human) == true`,
`AllowsPair(human,human) == false`, pending conversion inactive until successful
revival, and timeout choosing Humans while an unconverted Human is dead.
All checks use the repository's linked production code rather than a second
simulated match implementation.


## Execution findings

The command-start regression failed before implementation. Subsequent regressions
found and fixed stale assist contributions after infection, any-death setting
changes inside a death callback, and premature recovery during nested lethal
reflection. Independent review also caught accidental Unicode transcoding in
existing panel labels; restored their original catalog keys. Activation checks
now revalidate alive/ready receipts after the settings notification.

Final cleanup is deliberately deferred to the next match tick, not LeaveDeath:
a reflected inner death can complete before the outer lethal hit calls Die.
Infection still commits before the deadline and blocks new opposing-team writes.

Validation: release build clean; runtime 2,688 plus 69 Bat checks; executable
combat hooks 4,871; portable/catalog/native compatibility runner passes.
Full commands and limits are recorded in docs/zombie-survival.md. No deployment
or live host/guest validation was performed.
