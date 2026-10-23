# Periodic PvP Recovery Plan

User-approved defaults: enabled, 3 seconds. Keep the rolling hit-history window
separate: it measures recent cadence; the new interval periodically restores
shared burst capacity and clears all cadence categories for an attacker/opponent.

Use one lazy clock in the existing connection/life/floor-bound `BalanceWindow`.
Anchor at its first eligible hit, retain fixed deadlines during continuous fire,
and collapse missed periods into one recovery without banked credit. Live toggle
or interval edits rearm without granting immediate credit. Preserve continuous
refill, role scaling, lower-bound assistance, native defenses, artifact/DoT clocks.
Invalidate old in-flight refunds when recovery replaces the reserve. Reuse hit
history arrays; no timer jobs, scene scans or allocation on warmed resets.

- [x] Regression tests for uninterrupted fire, exact boundaries, independent
  pairs, mixed sources, live edits, reentrancy, lifetimes and allocations.
- [x] Settings/commands/preset v51 and clearly distinct EN/KO UI controls.
- [x] Runtime recovery through shared combat infrastructure, default on/3s.
- [x] Non-deploying build and relevant combat/runtime/native/UI tests.
- [x] Document behavior and DPS tradeoff. No deployment.

Release 0.92.0: build clean, 15,654 combat and 14,193 runtime checks passed,
with portable settings/catalog and installed-game/UI contracts verified.
Review fixes cover fractional duplicate deadlines, unchanged assistance refunds,
and explicit recovery-reset availability during compatibility failure. Warmed
recovery measured zero managed allocation in 3,000 fixture hits. Commit and push
follow verification under the repository's standing workflow.
