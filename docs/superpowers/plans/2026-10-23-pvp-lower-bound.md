# PvP lower-bound assistance implementation plan

> Use the subagent-driven-development workflow for isolated settings/presentation
> work and a separate final review. Keep core damage accounting under one owner.

**Goal:** Help weak positive PvP hits remain useful without giving every rapid
packet a fresh percentage-of-HP minimum or changing native PvE/player stats.

**Architecture:** Measure each hit's native-resolved, percentage-scaled damage
relative to its opponent's maximum HP. Share a bounded bonus allowance across all
sources for an attacker/opponent, followed by existing frequency/budget/role/shield
handling. Reuse the pair lifecycle and budget arithmetic; no roster scans.

**Tech stack:** C#, existing Unity/Mirror/Harmony hook, policy/preset/chat/UI layers,
executable combat and runtime fixtures, installed-game compatibility checks.

## Model and constraints

- Optional, default off. User-confirmed tuning: reference 4% of victim max HP,
  assistance refill 8% max HP/s. Both values remain configurable.
- Let D be valid positive native-resolved damage after global PvP percentage;
  R = victim max HP × reference%. Requested bonus = max(0, R - D/2).
  Thus with full assistance D becomes max(D, R + D/2), retaining offensive ordering.
  It is continuous at D=2R; higher hits are unchanged. Zero/invalid damage is excluded.
- For 150 HP and 4% reference, D=1 requests 5.5 extra (6.5 total), D=2 requests
  5 extra (7 total), D>=12 is unchanged. These are pre-role/pre-shield examples.
- Apply existing hit-frequency factor to BOTH original damage and requested bonus.
  Bonus tokens are measured in these post-frequency, pre-role units. One pool has
  capacity R and configured refill, shared by direct/incidental/Planet/proc sources.
- The total then uses the EXISTING upper budget. Prefer spending original damage
  first, immediately refund bonus credit that the upper budget cannot fund, and
  refund unused funded bonus on later native absorption/veto exactly once using
  the existing scope/generation guards. Shields that absorb damage count as use.
- Reserve before native callbacks; nested hits cannot reuse credit. Never turn
  suppressed DoT/artifact pulses or failed hits into damage. Empty bonus credit
  leaves the original hit intact; existing minimum-1 semantics stay unchanged.
- Use original victim maximum HP (not attacker's HP, current HP, or live stat writes).
  This is opponent-relative damage pressure, not a guessed global gear score.
  Native defense already contributes to D; assistance partly compresses that
  resulting gap. Later role reductions/shields are NOT bypassed or compensated.
- Lifecycle resets: connection receipt, player life, floor, selected participants,
  match/KDA/combat reset. Ordinary config edits/off-on preserve spent upper credit;
  bonus capacity/rate edits use existing conservative refill arithmetic.
- No per-frame work, source-specific bonus pool, per-hit allocation, extra RPC,
  guest installation or deployment. Commit/push after verification as authorized.

## Task 1: settings, commands, persistence and UI

- [x] Test parser acceptance/rejection, default off, bounds, sibling edits,
  match WithEnabled overlay, reset and v49 preset round trips before implementation.
- [x] Add PvpLowerBoundSettings / PvpLowerBoundChange: Enabled, ReferencePercent,
  RefillPercent, ReferenceScale, RefillScale, invariant formatted numbers,
  HasChanges. Defaults 4 / 8; reference 0.1..25%, refill 0.1..100%/s, two decimals.
- [x] FriendlyFireSettings.LowerBound and FriendlyFireCommand.LowerBound; commands
  `/one friendlyfire lower-bound on|off|reset`, `reference N|reset`, `refill N|reset`.
  Integrate validation, status and preset v49; preserve unrelated settings and
  active matches. No change to CombatBalanceRevision for bonus-only edits.
- [x] PvP Balance UI toggle and exact-value fields/Apply/reset, EN/KO text. Use
  existing draft and cleanup; make left pane scrollable if needed, retain layout
  of existing controls and full right DoT list. Explicitly explain shared bonus
  allowance and remaining role/shield/main-budget limits.
- [x] Build/runtime/localization/policy checks with DeployMod=false.

## Task 2: runtime integration and regressions

- [x] Add pure requested-bonus helper and failing executable regression cases.
- [x] Add bonus budget in BalanceWindow, reset with existing budget lifetime.
  Calculate optional frequency once, cap bonus, feed combined damage to upper
  budget, reserve/refund bonus without expanding upper credit.
- [x] Track actual funded bonus in current damage scope for native-loss refunds.
- [x] Cover weak/strong/zero/invalid inputs, tiny scales, all shared sources,
  rapid toggle off/on, exhausted upper/bonus budgets, roles, shields, nested hits,
  participant/life/reconnect/floor/settings resets, PvE/off preservation.
- [x] Simulate equivalent slow/multi-hit damage and bounded bonus over elapsed
  time. Warmed hit loop must allocate zero; report cost/limitations honestly.

## Task 3: integration, documentation and review

- [x] Document alternatives (flat floor, global stat normalization, chosen bonus
  budget), math/examples, percentage-scale limitations and live verification.
- [x] Independent review of accounting, config lifecycle, and scope. Fix findings.
- [x] Verify Release build, combat/runtime/localization/policy/native contracts.
- [x] Version bumped to 0.90.0; all verification passed. No deployment.
- Final conventional commit/push is recorded in Git history and the ignored workflow ledger.

## Verification outcome

Independent accounting/lifecycle/UI review: no actionable findings. Release build:
0 warnings/errors. Friendly-fire executable hooks: 15,593 checks; runtime command/session:
14,144 checks; localization: 50; panel: 12 lifetime + 16 geometry checks. Policy and
installed-game contracts passed, including 267 lower-bound and 4,589 catalog checks.
Warmed assistance loop: 0 allocated bytes / 2,000 hits. Live Unity/guest verification
is still pending. See ../../pvp-lower-bound.md for tuning and boundaries.
