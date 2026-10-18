# Zombie Survival (0.64.0, 2026-10-18)

Host-controlled team mode built on `CombatMatchRuntime`, alongside Deathmatch,
Tournament and Play Tag. Uses native damage, death, revival, chat/bubbles and
cosmetic transports; guests do not need new assets, packets or addon state.
Automated/native checks are not proof of live stock-guest rendering or gameplay.
Nothing was deployed or launched during implementation.

## Controls

Open the **Zombie / 좀비** tab. It includes the existing participant editor,
start/stop and K/D/A reset, round duration, optional any-death infection, and
independent Zombie refill/incoming-damage controls. Equivalent host commands:

| Command | Behavior |
| --- | --- |
| `/one zombie start` | Capture eligible participants and begin a three-second countdown |
| `/one zombie stop` | Cancel the match and recover pending dead participants |
| `/one zombie status` or `help` | Show state/settings or command syntax |
| `/one zombie duration 120` | Next match's duration; 3..3600 whole seconds, default 120 |
| `/one zombie duration reset` | Restore 120 seconds |
| `/one zombie any-death on` / `off` / `reset` | Convert on any completed Human death; default/reset off |
| `/one zombie refill on` / `off` / `x2` / `reset` | Zombie refill multiplier, initially on, default x2, range 0.1..10 |
| `/one zombie damage on` / `off` / `75%` / `reset` | Zombie incoming PvP damage, initially on, default 75%, range 0..100 |
| `/one zombie resetkda` | Reset shared K/D/A and matchup records; keep teams and timer |
| `/one save` | Persist settings only; nondefault Zombie settings use preset v31 |

Both multipliers accept up to two decimal places and apply live. Refill multiplies
the global PvP damage-budget refill and requires PvP balancing. Incoming damage
follows Play Tag semantics: it applies to admitted opposing-player hits even
when global balancing is off. It never affects PvE damage. Neither modifier
changes native character stats. Edits do not reset or replenish spent budgets.

## Match rules

- An empty explicit selection means everyone. Otherwise only selected current
  connections enter. Require at least two ready participants. After recovery and
  countdown, choose one living entrant uniformly as the initial Zombie.
- Only opposing teams can damage one another. Existing shared hostility filters
  cover companions, artifacts, debuffs, reflection, homing and auto-targeting.
- A confirmed Zombie kill infects a Human. Infection immediately revokes their
  old targeting permissions; the new Zombie becomes combat-active after a
  successful native revival. Zombies remain Zombies after death.
- Every participant uses the shared one-second respawn. With **Any death
  converts** off, environmental/enemy deaths preserve the Human role. When on,
  any successfully completed Human death infects. The setting is captured at
  death entry so edits inside native callbacks affect later deaths only.
- A completed final infection before the deadline commits Zombie victory.
  Otherwise, timeout gives Humans victory if any Human remains unconverted,
  including a Human waiting for respawn. Pending infected players count as
  Zombies for this outcome and final role reporting.
- Conclusion/recovery occurs on the next match tick, after native death/attack
  callbacks finish. New hostile actions are already blocked at final infection
  or expiry. This catches lethal reflection where the inner Human death precedes
  the enclosing attack's Zombie death; neither player is left outside recovery.
- Results report every entrant as `Name - Zombie/Human (K/D/A)`. A cancellation
  says the match stopped instead of declaring a winner. K/D/A uses the existing
  shared attribution and callback-invalidation rules.

## Lifetimes and extension points

`ZombieSurvivalRound` holds the pure infected/pending model. The shared-runtime
partial `CombatMatchZombie` owns roster receipts, initial role selection,
conversion, deadline and result formatting. It uses existing native death/revive
finalizers, respawn queues, `ProtectDeath`, match tokens and scope validation.
Death/revive exceptions cannot activate an incompletely restored Zombie.

`CombatMatchRoster.CaptureRoster` is shared with Play Tag. A roster records
connection receipts, not just player IDs. Late arrivals are spectators. Any
enrolled connection departure/replacement cancels the match, so reconnecting
players never inherit an old team. Participant-list changes or touching the
master friendly-fire toggle also cancel. Normal stop recovers pending dead
players; authority/run replacement discards stale work rather than reviving into
a different run. Loading delays respawn but does not pause an active deadline.

`CombatRoleSettings` generalizes Tag's former chaser settings/parser.
`CombatMatchRoles` selects current role values at resolved PvP hits, and
`RoleRevision` protects conservative refill accounting through setting/team
changes. Tag and Zombie retain independent configuration. Conversion removes a
player's old assist contributions against their new teammates without clearing
accumulated scores, matchups or unrelated pending death notices.

No role, roster, timer, score or pending conversion is serialized. Preset v31
stores only nondefault settings; v1..v30 load Zombie defaults and reject new
Zombie rows. Strict parsing rejects duplicate/invalid rows before atomic apply.

Unchanged match observation allocates no per-frame state in the runtime fixture.
Roster scans are bounded by session size, respawn observation reuses the existing
0.1-second cadence, and timer messages occur every 30 seconds and in the final
10 seconds. No new coroutine, network snapshot stream or client polling loop.

## Verification and live checks

Release build: zero warnings/errors. Runtime suite: 2,688 integration checks plus
69 Bat lifecycle checks. Friendly-fire executable hooks: 4,871 checks. Full
portable/catalog/installed-game compatibility runner passes, including strict
presets, EN/KO parity, native death/revive/transport contracts, team policy, UI
wiring and existing Tag/Tournament/Deathmatch regressions. Every build/test uses
`-p:DeployMod=false`.

Regressions include pending-Human timeout survival, last-infection deadline,
one-second/manual revival, stale reconnect callbacks, all interruption paths,
conversion assist cleanup, any-death edits inside callbacks, activation readiness
changes and nested lethal reflection cleanup. Stable Zombie ticks allocate zero
bytes across 1,000 fixture observations.

Live verification remains: host plus stock guests in lobby and dungeon; opposite
team direct/debuff/companion/homing hits; death and revival presentation; team
conversion announcements; timeout with a dead Human; cancellation/rejoin; panel
readability in English and Korean. Existing native transport is reused, but these
end-to-end checks were not performed.

See the [design](superpowers/specs/2026-10-18-zombie-survival-design.md),
[implementation plan](superpowers/plans/2026-10-18-zombie-survival.md),
[Play Tag](play-tag.md), [friendly fire](friendly-fire.md), and
[PvP balancing](pvp-balancing.md) for the underlying mechanisms.
