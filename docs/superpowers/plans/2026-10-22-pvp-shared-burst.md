# PvP Shared Burst Implementation Plan

**Goal:** Preserve infrequent heavy hits without giving each small packet a fresh damage curve.

**Architecture:** Remove the unconditional 5%-to-20%-HP per-hit curve. Feed resolved, PvP-scaled damage through the existing optional cadence reduction and shared attacker/opponent allowance. Default stored allowance becomes 75% max HP; sustained refill stays 70%/s. Direct attacks, spells, artifacts, companions and admitted debuffs share one pool.

**Tech stack:** C#, Harmony, existing net10 native-shaped fixtures and installed-assembly checks.

## Constraints

- Host-only addon; native damage/replication, shields and PvE remain unchanged.
- Preserve positive-hit minimum, funded-only refunds, live edits, role scales and lifecycle retirement.
- No extra per-frame work, allocations or weapon-specific counters.
- Preserve explicit preset burst/refill values; omitted burst values use the new default. Existing explicit 20% must be changed/reset to allow larger isolated hits.
- Build/test with `-p:DeployMod=false`; commit/push only after verification; no deployment.

## Implementation

- [x] Add `SharedBurstTests.cs` to the executable combat suite. At 150 HP, 75% reserve, 300%/s refill and frequency off, assert 100 -> 100 and 130 -> 112.5, including recovery after 2.5s. Compare equal-total simultaneous packets, mixed-source exhaustion, minimum hits, native defenses and default configuration. Run and confirm failure against the old curve.
- [x] In `FriendlyFireRuntime.Scale`, pass `scaled` directly to `LimitBalancedDamage`. Delete the obsolete `FriendlyFireBalance` helper. Set `PvpRapidSettings.DefaultBurstPercent = 75m`, derive integer offsets and the pure budget default from it. Keep the budget algorithm and all lifecycle state unchanged.
- [x] Update old curve-specific expectations and retain explicit 20% configurations in legacy budget/lifecycle tests. Verify settings/presets retain explicit values, command reset uses 75%, and omitted values adopt 75%.
- [x] Update Rapid Hits panel, command help/status and EN/KO catalogs to explain shared burst versus continuous refill without claiming per-hit compression. Rewrite the standalone `--burst-audit` as a before/after diagnostic. Document new behavior and known minimum-one tradeoff in feature docs; bump version to 0.88.0.
- [x] Run combat fixtures/diagnostic, policy/native compatibility, runtime and localization suites; verify warm-path allocation checks. Review diff and commit as `fix(pvp): preserve heavy hits with a shared burst allowance`, then push main.

## Verification record

- Heavy-hit regression failed against the old curve, then passed after removal.
- Release build: zero warnings/errors; deployment disabled.
- Combat: 9,222 checks; diagnostic: 26; runtime: 14,047 plus 69 Bat checks; localization: 50.
- Policy/preset and installed-game contract suite passed, including explicit legacy burst and current reset values.
- Warmed combat probes: zero allocated bytes. Fresh reviewer found no production blocker; partial max-HP reserve coverage was strengthened and passed.
- Live gameplay/network rendering is not verified. No deployment.
