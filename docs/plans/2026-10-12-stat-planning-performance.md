# Stat planning performance follow-up

User request: optimize without changing behavior or weakening architecture.
Baseline: `63e1ab1`, addon 0.51.0. No deployment.

## Evidence and scope

The existing five-player fixture allocates 0 B per unchanged synchronization
tick. Full reports allocate 138,640 B; ordinary tabs already capture only their
displayed sections. Combat identity checks intentionally run again after native
callbacks, and match roster scans already reuse buffers. Keep these paths intact.

`StatPlanner` eagerly formats range and representability errors on successful
plans. `SessionPolicy`, relative recovery and fruit planning wrap a single value
in an input array and receive a one-element output array. Native bonus/amplifier
changes repeat this work for each retained relative setting.

## Design

Expose a scalar `StatPlanner.TryPlan(StatCommand, StatSnapshot, out StatUpdate,
out string)` overload. Move stat-selection validation into it so scalar and batch
callers share every check. Keep the batch overload's all-or-nothing output and
empty-roster handling. Preserve exact native arithmetic, binary search, reset
behavior, error text, validation order and native multiplier fallback.

Create formatted error text only in the failing branch. Reuse the scalar overload
from session policy, relative reset recovery and fruit-budget planning. Do not
add caches, retain native snapshots, change network writes or change polling.

## Execution and verification

1. Extend `tests/SephiriaOne.RuntimeTests/PerformanceProbe.cs` with successful
   relative planning and five-player native-stat-change workloads. Measure the
   current code; require zero allocations for successful scalar policy planning.
2. Add the scalar overload in `Features/Stats/StatPlan.cs`, route the three
   single-value consumers through it, and defer error construction to failures.
3. Add scalar/batch parity coverage for every catalog stat, reset, invalid stat
   selection, overflow, range, rounding and native penalty fallback; run existing
   policy, resource, localization and runtime transition regressions.
4. Run Debug/Release builds with `-p:DeployMod=false`, portable/native contract
   tests, runtime `--perf-check`, friendly-fire and Tournament suites. Record
   before/after allocations (fixture timings are not live Unity FPS).
5. Obtain an independent review, document results in `docs/performance-review.md`,
   bump patch version, then commit and push using the repository workflow.

Commands: `dotnet run --project tests/SephiriaOne.RuntimeTests -c Release
-p:DeployMod=false -- --perf-check` and the build/test commands in the existing
performance review. Successful policy planning should allocate 0 B; existing
allocation budgets and behavior suites must still pass.
