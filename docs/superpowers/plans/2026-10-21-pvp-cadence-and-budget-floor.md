# PvP cadence isolation and responsive budget floor

**Goal:** Implement recommendation 3 from the low-scale fairness audit and the
explicit follow-up that budget exhaustion must not discard otherwise valid hits.

**Design:** Two bounded recent-hit histories per directed player pair: direct
player attacks versus incidental debuff, companion, reflection and artifact
damage. Both feed the same existing damage allowance. Classification uses the
captured source/context and native type/ID tags; no scene lookup, stack capture,
per-hit dictionary of effect IDs or per-frame task.

The existing optional square-root frequency formula stays unchanged. The budget
continues clipping damage continuously to remaining credit but retains the
positive minimum (one final pre-shield damage) at zero credit. Reserve only
funded credit, cap refunds to that reservation and never create debt/credit from
the unfunded floor. The floor is an intentional exception to a strict DPS cap;
arbitrarily fast one-point hits can exceed the configured refill. No delayed
damage or catch-up queue. Existing native blocks, invalid/zero hits, explicit
zero damage settings and debuff/Dark Cloud schedules remain intact.

Alternatives: a delayed damage queue violates responsiveness/lifetime requirements;
global exponential load estimation changes more tuning and costs more per hit.
The bounded continuous clamp plus minimum is the smallest compatible change.
Other fairness recommendations remain deferred.

- [x] Add failing cadence/exhaustion tests to the executable combat suite.
- [x] Split frequency history with shared reset ownership and cheap classification.
- [x] Update budget/finalizer accounting for funded versus minimum-only damage.
- [x] Adjust old strict-cap expectations; test nested callbacks, shields,
      floor-only refunds, roles, lifecycle, DoT schedules and both hot paths.
- [x] Update docs/localized help, verify Release/fixture/native compatibility
      with `-p:DeployMod=false`, request independent review.

Delivery follows the repository's conventional commit/push workflow. No deployment.

## Verification

Release `0.77.0` built with zero warnings/errors. Executable combat checks:
6,222 passed, including the initially failing exhausted-budget regression.
Shared runtime/session checks: 13,704 plus 69 Bat lifecycle checks passed.
Localization: 50 checks passed; the full portable policy/catalog and installed-game
compatibility suite passed, including compiled frequency-before-budget ordering
and 33 Harmony API references. Independent review found no actionable issues.
Warmed direct/incidental traffic allocated zero bytes across 2,000 executable
hits. No new frame task, hit log, network message or per-effect collection.
These checks do not establish live Unity/stock-guest balance; no game was launched.

Native evidence: REA summary `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`
reconfirmed the installed Assembly-CSharp hash documented in domain findings.
Matching decompilation: `EDamageFromType` is None/DirectAttack/Magic/Trap;
`DamageInstance` defaults to Magic; Frostium Ring's callback emits None.
Existing artifact projectile identity guards document Charm-prefixed None
projectiles; source scope catches synchronous artifact damage. This is a hit
cadence approximation, not a native attack-activation/cooldown model.
