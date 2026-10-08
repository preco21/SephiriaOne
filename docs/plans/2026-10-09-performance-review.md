# Status-report allocation optimization plan

**Goal:** Reduce measured report-construction overhead while preserving current
commands, text, fresh reads, snapshot ownership and synchronization behavior.

**Baseline:** `df0b920` (0.36.0). Five-player Release fixture: unchanged sync
0 B/tick in all three workloads, full report 195,602 B/refresh, compact snapshot
25,433 B/refresh. Timings are informational, not live Unity performance evidence.

## Design and scope

Keep gameplay reconciliation and its per-frame readiness/observation/write guards
unchanged. Current names, resources, damage hooks and event spawns already avoid
per-frame allocation or run only at event boundaries. Inventory safety scans and
merchant placement/order must retain their current behavior.

Optimize the measured reporting path with call-local scratch buffers:

1. Represent an immutable reconciliation status as a value and allow callers to
   append statuses to their own list. Keep `Describe` as an independent snapshot
   API. Diagnostic reads must not create subjects, capture inputs, apply rules,
   acknowledge invalidations, or advance revisions.
2. Reuse one status list and text builder within each diagnostic report. Build
   player stat rows directly with a local builder. Preserve exact text, ordering,
   localization lookup, numeric culture, empty details and historical fault text.

Persistent rendered-text caching would need additional invalidation for native
edits and localization; changing poll frequency could miss critical boundaries.
Neither is needed for this optimization. Buffers remain local to each invocation,
so nested reads and retained snapshots do not share mutable state.

## Work and validation

- [x] Review runtime/update paths and capture the current allocation baseline.
- [x] Add a full-report allocation ceiling and show it fails before optimization.
- [x] Add regression coverage for read-only diagnostics, state order/revisions,
  independent snapshots, unknown/forgotten subjects and reusable status capture.
- [x] Optimize status transport and report formatting without gameplay changes.
- [x] Verify diagnostic output including fallback/fault and EN/KO behavior, run
  lifecycle fixtures and compare before/after allocation measurements.
- [x] Run Debug/Release builds, portable/runtime/localization suites and installed
  game compatibility, with `-p:DeployMod=false` on every dotnet command.
- [x] Obtain independent review, record results in `docs/performance-review.md`,
  increment patch version, commit using Conventional Commits, and push `main`.

No deployment, new dependencies, new polling, network changes or live gameplay
claims. Preserve unrelated files; no game binaries/decompilation artifacts enter
the repository.

## Results

- Full report: 195,602 -> 116,558 B/refresh, a 40.4% reduction. The new
  160 KiB ceiling failed before changes and passes after them. Compact capture
  remains about 25,433 B/refresh; unchanged sync remains 0 B/tick.
- Runtime checks: 1,250, plus 69 Bat lifecycle checks. Portable suite includes
  53 shared reconciliation checks. Localization engine: 48 passed, zero failed.
- Debug/Release production builds: zero warnings/errors. Installed-game
  compatibility checks passed. Independent review found no actionable issues.
- Verification exercises game API fixtures and native contracts, not live Unity
  frame times. Deployment remains disabled.
