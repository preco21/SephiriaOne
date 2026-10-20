# Minority-side Zombie dynamic balancing

**Goal:** Neutral even teams, modest minority-Zombie help, stronger minority-Human
offense and defense, with bounded smooth transitions and unchanged static mode.

**Design:** Reuse the match-owned smoothed Human share `s = H / (H + Z)`.
Use signed imbalance `b = 2s - 1` and smoothstep strength
`u = |b|² (3 - 2|b|)`. This is continuous with continuous first derivative at
parity; team ratios saturate toward finite endpoints rather than raw multipliers.

For `b >= 0`: Zombie refill `1 + 0.5u`, incoming `1 - 0.2u`, Human incoming 1.
For `b < 0`: Zombie refill `1 - 0.4u`, incoming `1 + 0.6u`, Human incoming
`1 - 0.4u`. Human offense improves through increased Zombie incoming damage;
Human refill remains native. Bounds: Zombie refill 0.6..1.5, Zombie incoming
0.8..1.6, Human incoming 0.6..1. Existing independent switches apply: refill
switch controls Zombie refill; incoming switch controls both teams' incoming
adjustments. Dynamic off restores exact manual Zombie values and neutral Humans.

The existing one-second exponential smoothing/counting/lifetimes remain. Cache
the polynomial outputs when share advances, and use one existing roster lookup
to identify either live team. No new per-frame task, roster scan, stat writes,
per-hit allocations or packets. Pending conversions count in composition but
receive no combat modifier until revival. The shared damage floor remains.

Alternatives: reducing only Zombie refill would leave Humans exposed to initial
bursts and balancing-off hits; rewriting live stats would affect PvE. Targeted
incoming modifiers use the established PvP boundary and preserve item mechanics.

- [x] Add failing golden-ratio, parity, minority, smoothing and lifecycle tests
  in `ZombieDynamicBalanceTests.cs`; add real damage/refund/PvE checks in
  `ZombieCombatTests.cs` with both sides participating.
- [x] Update `ZombieDynamicBalance.cs`, `CombatMatchRoles.cs`, and the shared
  active-role lookup in `CombatMatchZombie.cs`; keep static/manual behavior.
- [x] Show Human protection in dynamic status/UI, update EN/KO help and docs,
  and verify Release, runtime, damage hooks, localization and native compatibility
  using `-p:DeployMod=false`.
- [x] Independently review the implementation and damage-budget integration.

**Verification (0.78.0):** Release build succeeded with zero warnings/errors,
with deployment disabled. Runtime tests passed 13,753 checks plus 69 Bat checks;
friendly-fire tests passed 6,227 checks. Localization tests passed 50 checks,
panel tests passed 12 plus 16 checks, and the portable/native compatibility suite
passed, including 4,177 catalog checks and 33 Harmony API references. The warmed
20,000 paired Zombie/Human role queries allocated zero bytes. Independent review
found no actionable issues. Live multiplayer balance testing remains necessary.

Delivery: conventional commit and push on the current branch; never deploy.

Balance remains an initial tuning choice: bounds cannot guarantee a particular
TTK against arbitrary native builds, crowd control or multi-player focus fire.
