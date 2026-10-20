# Zombie Dynamic Balancing implementation plan

**Goal:** Optional, host-authoritative Zombie team modifiers that follow team
composition without changing native stats or other combat modes.

**Design:** Use Human share `s = H / (H + Z)` (equivalent to the bounded ratio
curve `r / (1 + r)`). Refill is `1 + 2s` (bounds ×1..×3); incoming damage is
`1 - 0.7s` (bounds 30..100%). Equal teams yield ×2 / 65%; 1:5 yields ×2.67 /
41.67%; 5:1 yields ×1.33 / 88.33%. Smooth share changes exponentially with a
one-second time constant (95% of a change after three seconds). Set the initial
match composition immediately; settle the old target before retargeting at a
conversion. Pending respawns count by committed team, not alive state.

The toggle defaults off, uses `/one zombie dynamic on|off|reset`, and persists
in v41 presets. Existing per-modifier enable flags remain authoritative; manual
numbers are preserved and resume immediately when disabled. Refill still needs
global PvP balancing. Incoming remains opposing-player-only, matching static
role modifiers. No extra network messages, live-stat mutations or per-hit roster
scans. Mode stop, scope replacement and reconnect use existing cleanup.

Alternative direct ratio scaling is unbounded; discrete ratio bands create
jumps. The bounded share curve and temporal smoothing avoid both.

- [x] Add executable policy/runtime regression coverage and observe failure.
- [x] Add a small pure smoothing policy under Combat/ZombieSurvival; integrate
      it with role queries and conversion/start/clear events.
- [x] Wire policy, host commands, v41 preset validation, compact UI and EN/KO.
      Dynamic readouts replace editable manual numeric inputs while enabled.
- [x] Verify curves, transition continuity, cleanup, 9-player roster, budgets,
      old presets and disabled behavior; build with `-p:DeployMod=false`.
- [x] Record findings and review the diff independently.

Verification completed 2026-10-21: Release build clean; runtime 13,704 checks,
combat hooks 6,194 checks, full policy/catalog/native compatibility suite,
localization and panel suites passed. Independent reviewer found no actionable
issues. Final delivery follows the repository's conventional commit/push policy.

Live unmodified-guest behavior remains a manual validation step; the feature
uses existing host damage decisions and introduces no guest dependencies.
