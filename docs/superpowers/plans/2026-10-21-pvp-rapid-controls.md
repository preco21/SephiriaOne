# Configurable PvP rapid-hit controls

**Goal:** Expose actual hit-history and continuous-budget parameters without
adding periodic resets, changing defaults, discarding valid hits or increasing
per-hit storage. Host-only shared commands/settings, EN/KO, saved presets.

## Policy and semantics

`PvpRapidSettings` is an immutable default-safe value in
`FriendlyFireSettings.Rapid`. Controls (all at most two decimals):

| Property / command key | Default | Range | Meaning |
| --- | --- | --- | --- |
| WindowSeconds / window | 1s | 0.1..10s | Rolling hit history, eight bounded buckets. |
| HitsPerSecond / rate | 4 | 0.1..100 | Full damage until count exceeds max(1, rate × window). |
| Exponent / exponent | 0.5 | 0..2 | Above threshold, damage × (threshold/count)^exponent. Zero disables this reduction. |
| BurstPercent / burst | 20% | 1..100% | Maximum stored allowance as percent of victim maximum HP. |
| Existing refill | 70%/s | Existing 1..300%/s | Continuous replenishment, independent of hit-frequency toggle. |

No separate reset timer exists. Show empty-to-full time = burst/refill; explain
that per-hit softening, native defenses, role modifiers and DoT schedules remain
separate. There is no strict window damage cap because positive minimum hits can
exceed the budget. Direct and incidental histories stay separate; budget shared.

Commands: `/one friendlyfire rapid window|rate|exponent|burst N|reset` and
`/one friendlyfire rapid reset` for these four parameters only. Existing
`hit-frequency on|off|reset` retains its toggle-only semantics. Existing refill
command remains unchanged. Numeric edits do not stop modes or reset scores.

## Interfaces and ownership

- Policy/session/presets: `PvpRapidSettings` properties above plus double
  `WindowScale`, `RateScale`, `ExponentScale`, `BurstScale`, invariant number
  strings, `HasChanges`, `SameFrequency(other)`. Constructor with named
  `windowSeconds`, `hitsPerSecond`, `exponent`, `burstPercent` defaults above.
  `FriendlyFireSettings` adds final optional `rapid = default`; command adds
  typed nullable rapid change. Preset v44 stores only nondefault rapid values.
  Increment CombatFrequencyRevision on effective window/rate/exponent edits,
  never on capacity/refill alone; no CombatBalanceRevision bump for these edits.
- Runtime: `FriendlyFireHitFrequency.Next(now, settings = default)`; defaults
  retain sqrt fast path. `FriendlyFireDamageBudget.Take` appends optional
  capacity argument; retain old calls. Track prior capacity and refill so edits
  cannot retroactively manufacture credit. Refunds clamp to current/last cap.
  Existing weak receipt, life/floor/mode cleanup and minimum-positive-hit rules.
- UI: separate **Rapid Hits** page beside PvP Balance, same window/controller,
  SnapshotContent.None. Detection/threshold/strength left; capacity/refill and
  recovery explanation right. Shared Apply and draft lifecycle, no extra polling.

## Steps

- [x] Policy/parser/session/preset tests first; implement new immutable settings,
  strict command validation, v44 atomic parsing and backward compatibility.
- [x] Add failing core/runtime tests for configurable history, strength, budget
  edits/refunds/minimums, lifetimes and zero warmed allocation; implement policy.
- [x] Add UI, help/status and EN/KO; update route/draft/native contracts.
- [x] Non-deploying Release build and relevant suites; independent review;
  record findings, version, conventional commit and push.

Alternatives: a periodic reset would grant sudden repeated bursts and change
the current model. A single combined history/budget window would conflate two
independent controls. Retain the current mechanisms with explicit units instead.

## Completion evidence (2026-10-22, 0.83.0)

User confirmed continuous refill and unchanged defaults. Release build: zero
warnings/errors, deployment disabled. Combat fixtures: 8,914 checks; runtime
session fixtures: 13,868 plus 69 Bat checks; policy: 373 new rapid checks;
catalog: 4,423; localization: 50; panel: 12 lifetime + 16 geometry; installed
native contracts and 40 Harmony API references passed. Warmed configurable
rapid hits allocate zero bytes. Independent review found no actionable issue.
No game launch/deployment or live Unity/stock-guest validation was performed.
