# Zombie Survival (0.78.0, 2026-10-21)

## Dynamic Balancing (0.76.0)

The Zombie tab has a **Dynamic Balancing / 동적 밸런싱** toggle, default off.
Host commands: `/one zombie dynamic on|off|reset`; reset restores off.
`/one save` persists it in v41. Older presets remain static, and schemas v1..40
reject the new row rather than silently misinterpreting it.

Retuned in `0.78.0` to favor the minority side. The original curve still gave
Zombies ×2 refill / 65% incoming damage at parity and never favored Humans.
Even teams are now neutral; minority Zombies get a modest advantage, while
minority Humans get stronger offense and direct protection.

Let `s = Humans / (Humans + Zombies)`, signed imbalance `b = 2s - 1`,
and `u = |b|² (3 - 2|b|)`. This bounded smoothstep has no first-derivative jump
at parity; extreme ratios approach finite bounds with diminishing returns.

| Condition | Zombie refill | Zombie incoming | Human incoming |
| --- | --- | --- | --- |
| Humans ≥ Zombies | `1 + 0.5u` | `1 - 0.2u` | `1` |
| Zombies > Humans | `1 - 0.4u` | `1 + 0.6u` | `1 - 0.4u` |

| Zombies : Humans | Zombie refill | Damage Zombies take | Damage Humans take |
| --- | --- | --- | --- |
| 1 : 5 | ×1.37 | 85.19% | 100% |
| 1 : 1 | ×1.00 | 100% | 100% |
| 5 : 1 | ×0.70 | 144.44% | 70.37% |
| 8 : 1 | ×0.65 | 152.43% | 65.05% |

Bounds: Zombie refill **×0.6..×1.5**, Zombie incoming **80..160%**, Human incoming
**60..100%**. Human offense improves by making majority Zombies more vulnerable;
Human refill is unchanged. Direct Human protection also reduces initial bursts
and works independently of the optional global PvP-balancing toggle.

These are settled target values. Initial
roles initialize the curve immediately. Each conversion settles the previous
target up to that instant, then eases toward the new target exponentially with a
one-second time constant. The underlying Human share moves approximately 63% of
the way after one second and 95% after three seconds; the nonlinear multipliers
follow that share. Several conversions remain continuous, including crossings
of parity. Query/frame frequency does not change the curve.

Counts use the captured roster and committed team roles, including pending
respawns. A temporary death/loading state does not alter the ratio. A converted
corpse counts on its new team immediately, but combat modifiers only activate
after successful revival. Late joiners remain spectators; departure/reconnection
cancels through the existing connection-receipt lifecycle.

The existing switches still apply: refill controls Zombie refill, and incoming
controls **both teams'** incoming modifiers while dynamic balancing is enabled.
Numeric manual settings are retained; the UI shows read-only Zombie values plus
the effective Human incoming percentage in its status line. `/one zombie status`
also shows Human protection, with EN/KO text. Turning dynamic balancing off
immediately restores exact manual Zombie values and neutral Human modifiers.
Commands may edit dormant manual values without perturbing the active damage
budget. Outside a started round the dynamic model is neutral (×1 / 100%).

Refill still requires global PvP balancing; incoming scaling has the same
opposing-player scope as static Zombie modifiers. Native stats, PvE and other
modes are unchanged. Neither conversion nor smoothing replenishes spent damage
budget. Actual role/config transitions keep the existing conservative elapsed
refill protection; smoothing itself does not advance `RoleRevision` each frame.

`ZombieDynamicBalance` is a small pure, match-owned smoothing policy, consumed
by the shared `CombatMatchRoles` path. Start/conversion/clear own its lifecycle.
One shared active-role lookup identifies either live team through the existing
receipt-bound roster. Smoothed outputs are cached and recalculated only when
the share advances; repeated queries at the same time reuse them.
The implementation adds no network messages or per-query roster allocations;
guests receive the already-established host-authoritative damage results.

Verification covers curve bounds/monotonicity, frame-rate independence,
consecutive conversions, pending/manual respawns, nonconverting deaths, live
switches and dormant edits, 9-player starts, repeat matches, reconnect,
selection/master-toggle cancellation, scope/authority loss, timeout/victory,
strict preset migration, real damage-hook budgeting and PvE isolation. Warmed
20,000 paired role queries across both teams allocate zero bytes in fixtures.
Golden-ratio, neutral parity, both minority bounds, real damage/refund/minimum
tests and static restoration cover the retuning. No game was launched
or deployed; live guest gameplay and UI rendering still require verification.

This is bounded compensation, not equalized team DPS or a guaranteed TTK.
Builds, focus fire, shields and crowd control still matter. The established
one-point positive-hit minimum remains, so it can limit reductions at tiny
damage values. No native combat stats, attack mechanics or PvE damage are changed.

Current Zombies have a [persistent native green X indicator](zombie-role-visual.md),
including while awaiting revival. It is cosmetic, uses stock guest transport,
and clears with the shared match lifecycle. Live guest rendering remains to be checked.

Host-controlled team mode built on `CombatMatchRuntime`, alongside Deathmatch,
Tournament and Play Tag. Uses native damage, death, revival, chat/bubbles and
cosmetic transports; guests do not need new assets, packets or addon state.
Automated/native checks are not proof of live stock-guest rendering or gameplay.
Nothing was deployed or launched during implementation.

## Faction and conversion notices (0.66.1, 2026-10-19)

Use `[Z]` for Zombies and `[H]` for Humans in role labels, live participant rows,
status/countdown totals and results. These basic ASCII letters/brackets already
occur in native/addon chat; no biohazard/emoji glyph or extra font is required.
The marker is stable in both languages; the role words are translated. Pending
infection already displays `[Z]`, while combat activation still waits for revival.
Stale receipts, spectators, countdown-only entrants and stopped modes have no marker.

At activation, announce the actual initial Zombie names:

```text
[Z] "PlayerX" is a Zombie!
[Z] Zombies: "PlayerX", "PlayerY", "PlayerZ"
```

Names use existing safe-name handling. Multi-player lists pack complete quoted
names into messages of at most 120 characters for the stock chat receiver.
Only initial Zombies receive opening role bubbles; Humans receive none.
Opening recipients are captured before visual/chat callbacks. A Human infected
inside one of those callbacks keeps their conversion notice, and a dead initial
Zombie keeps their respawn notice instead of receiving an opening instruction.

Confirmed conversions use the killer name captured at death entry and the shared
respawn deadline. The current delay remains **one second**, not three:

```text
[Z] "PlayerX" infected you! Reviving as Zombie in 1s - hunt remaining [H] Humans!
[Z] "PlayerX"에게 감염됨! 1초 후 좀비로 부활 - 남은 [H] 인간을 사냥하세요!
```

Zombie-mode `Died` queues recovery but defers its initial announcement while a
native death scope is open. `ConfirmedKill` records the attributed infector;
`CompletedMatchDeath` handles opt-in environmental infection. `LeaveDeath` then
sends the resolved countdown once. A reentrant tick cannot restore/announce that
player before the death scope finishes. Conversion without an attributed killer
uses a neutral "Infected!" message instead of inventing a name.

Conversion fields belong to the existing per-life respawn entry, not a permanent
player map. The combined bubble replaces the generic countdown; no generic
floating countdown, separate revival role instruction or `Respawned!` bubble
overwrites it. Ordinary deaths and other modes keep their existing messages.
Normal K/D/A kill logs and private matchup records remain separate and unchanged.
Manual revival, stop, failed death/revival and session cleanup discard the entry.

Verification includes single/multiple names, a nine-player long-name roster,
EN/KO conversion length, Human opening silence, death-entry rename preservation,
environmental conversion, next-life cleanup, reentrant ticks, stop inside chat,
and infection during opening announcements. No live font/UI or guest session
was launched; stock transport contracts and executable fixtures are the evidence.

## Controls

Open the **Zombie / 좀비** tab. It includes the existing participant editor,
start/stop and K/D/A reset, round duration, starting Zombie count, optional any-death infection, and
independent Zombie refill/incoming-damage controls. Equivalent host commands:

| Command | Behavior |
| --- | --- |
| `/one zombie start` | Capture eligible participants and begin a three-second countdown |
| `/one zombie stop` | Cancel the match and recover pending dead participants |
| `/one zombie status` or `help` | Show state/settings or command syntax |
| `/one zombie duration 120` | Next match's duration; 3..3600 whole seconds, default 120 |
| `/one zombie duration reset` | Restore 120 seconds |
| `/one zombie count N` | Starting Zombies for the next match; positive whole number, default 1 |
| `/one zombie count reset` | Restore one starting Zombie |
| `/one zombie any-death on` / `off` / `reset` | Convert on any completed Human death; default/reset off |
| `/one zombie dynamic on` / `off` / `reset` | Derive modifiers from team composition; default/reset off |
| `/one zombie refill on` / `off` / `x3` / `reset` | Zombie refill multiplier, initially on, default x3, range 0.1..10 |
| `/one zombie damage on` / `off` / `30%` / `reset` | Zombie incoming PvP damage, initially on, default 30%, range 0..100 |
| `/one zombie resetkda` | Reset shared K/D/A and matchup records; keep teams and timer |
| `/one save` | Persist settings only; dynamic on uses v41, nondefault count v32, other Zombie settings v31 |

Both multipliers accept up to two decimal places and apply live. Refill multiplies
the global PvP damage-budget refill and requires PvP balancing. Incoming damage
follows Play Tag semantics: it applies to admitted opposing-player hits even
when global balancing is off. It never affects PvE damage. Neither modifier
changes native character stats. Edits do not reset or replenish spent budgets.

## Default tuning (2026-10-19)

Zombie defaults and reset values are now x3 refill / 30% incoming damage, both
still enabled. These are mode-specific defaults; Play Tag remains x2 / 75%.
Fresh sessions and preset rows with omitted values use the new defaults. Explicit
saved custom values remain intact, including an explicitly saved 50% from the
previous default, x2 or 75%. A saved 50% remains a nondefault row on the next save.
Use `/one zombie refill reset` and `/one zombie damage reset` to adopt the new
values over an existing override, then `/one save` if desired.

## Match rules

- An empty explicit selection means everyone. Otherwise only selected current
  connections enter. Require at least two ready participants. After recovery and
  countdown, choose the configured number of distinct living entrants uniformly
  as the initial Zombies. Clamp to 1..eligible participants minus 1 so at least
  one Human remains; one-player matches are still rejected.
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
- Results report every entrant as `Name - [Z] Zombie (K/D/A)` or
  `Name - [H] Human (K/D/A)`. A cancellation
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
stores the original nondefault settings; v32 additionally supports `zombie count`.
Older presets retain count 1 and reject the count row; v1..v30 also reject all
other Zombie rows. Strict parsing rejects duplicate/invalid rows before atomic apply.

## Starting count (0.66.0, 2026-10-19)

The host can configure `/one zombie count 3` or use **Starting Zombies / 시작 좀비 수**
in the Zombie tab. Any positive 32-bit integer is accepted, without allocating
anything based on the requested number. Capture clamps to the frozen eligible
roster, excluding spectators and late arrivals, and always leaves one Human.
For example, request 3 with two participants: one Zombie and one Human. The
stored preference remains 3 for a later larger match.

Duration and count are captured when Start is accepted. Changes during countdown
or active play affect only the next match; readiness recovery does not recapture
or reroll count. Actual roles are selected after recovery/countdown. A single
bounded roster scan samples uniformly without replacement, creates combat-active
initial roles (not pending infections), then reuses existing role visuals,
bonuses, role bubbles and team hostility. Announcement reports actual
starting count. No new RPC, guest state, native hooks or per-frame allocations.

Runtime regression coverage includes default, two and excess requested counts;
two- and nine-player rosters; selected participants/late arrivals; countdown and
active edits; cleanup/next match; shared bonuses/visuals/team hostility; strict
v32 save/read and old headers; reset, invalid input and host-only access. Seeded
pure-model checks verify exact distinct active teams and varying selections.
Activation-callback edits and readiness retries retain the captured count.
Release build, 3,134 runtime checks plus 69 Bat checks, 4,944 friendly-fire checks,
portable/localization checks and installed native/UI contract checks passed.
Live multiplayer behavior and UI rendering have not been exercised.

Unchanged match observation allocates no per-frame state in the runtime fixture.
Roster scans are bounded by session size, respawn observation reuses the existing
0.1-second cadence. Round-status messages use five-second remaining-time milestones
above five seconds, then update each second for the final five seconds. Repeated
ticks do not duplicate a notice; expiry sends the result instead of a zero-second
countdown. The three-second opening and one-second respawn countdowns already use
the final-five-second cadence. No new coroutine, network snapshot stream or client
polling loop.

## Verification and live checks

The 2026-10-19 tuning regressions cover fresh/current/older omitted preset values,
command resets, explicit prior 50% and 75% overrides, and active/converted Zombie
incoming scaling at 30%. They also retain the independent x3 refill and Play Tag
x2 / 75% defaults. These runtime fixtures passed with deployment disabled.
No live match was run.

Release build: zero warnings/errors. Runtime suite: 2,691 integration checks plus
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
