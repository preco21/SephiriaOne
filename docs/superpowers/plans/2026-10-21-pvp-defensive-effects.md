# PvP defensive effects implementation plan

**Goal:** Bound two newly identified defensive loops under the existing PvP
balancing toggle without changing native PvE defenses.

**Architecture:** Keep qualification in the shared hostile-hit/participant
receipt scope. Gate only Elastic Band's dash refund, once per defender every
2 seconds. Reuse the balance actor lifecycle for its cooldown. Adapt the native
counter count at the damage consumer using the existing weapon controller's
elapsed timer, allowing at most 0.35 seconds plus native latency allowance.
Never write native counter timers, invulnerability flags or artifact stats.

**Constraints:** Host-only; stock guests; no deployment; no frame polling or
inventory scans; preserve unrelated defensive callbacks and native short
counter windows. Finite saves/barriers and disabled items need no new patch.

- [x] Add failing executable fixtures for refund spam, multiple attackers,
  counter duration, nested PvE, connection/life/floor and setting resets.
- [x] Add `FriendlyFireDefensiveEffects.cs`, wire hooks and the narrow counter
  adapter into the existing installation/rollback path. Add one lazily used
  defender cooldown to `BalanceActor`.
- [x] Validate installed native IL and private field types, including changed
  contract rejection. Measure warmed hit allocation.
- [x] Document artifact identities, actual serialized values, retained effects,
  coverage boundaries and live-verification limits in `docs/pvp-defensive-effects.md`.
- [x] Run non-deploying build, combat/runtime/native compatibility/localization
  suites and independent review.

Verification: Release build 0 warnings/errors; 8,808 executable combat checks;
13,796 runtime checks plus 69 Bat checks; portable/installed-game IL contracts
including 4,332 catalog checks and 40 Harmony API references; 50 localization
checks. Independent review found no actionable defects; per-activation refresh
and latency limits are documented. Conventional commit/push follows verification.

Alternatives considered: suppressing dash refunds entirely would remove the
artifact's identity; mutating global counter duration would also weaken PvE.
The selected approach limits the problematic PvP paths only.
