# Synchronization performance review

## Follow-up: 0.22.2 (2026-09-29)

Reviewed session reconciliation and resources, name/chat controllers, UI and
localization, Rabbit/Merchant/Choices hooks, diagnostics and object cleanup.
The [implementation plan](plans/2026-09-29-performance-review.md) records scope.

Two concrete issues were fixed:

- Every open settings tab requested the full diagnostic report four times per
  second, although only Status displays it. Non-Status pages now omit report
  construction. They still read current player values and readiness/permissions,
  respect fault recovery, and refresh presets/language. Chat and Status keep the
  full report. No native values are cached across frames or intent revisions.
- Partially failed Choices unload cleanup retained a static journal and dungeon
  reference. Successful recovery cleared only the journal, and session teardown
  did not clear either feature reference. Both now clear after recovery or scope
  teardown, preserving recovery while that host scope remains active. Weak-reference
  tests reproduced both leaks and verify release after retry/reset, stop and scope
  replacement, including recovery without a later teardown to mask retention.

Release .NET 10 fixture measurements with five players and active settings:

| Snapshot construction | Bytes/refresh | Sample time/refresh |
| --- | ---: | ---: |
| Full status report (previously every tab) | 103,958 | 91.79 us |
| Non-Status panel pages | 27,880 | 25.77 us |

This removes about **73%** of snapshot allocations, roughly 304 KB/second at the
existing four refreshes per second while a non-Status panel is open. It does not
affect closed-panel gameplay. These figures measure snapshot construction, not
Unity/TMP rendering, actual GC pauses or FPS. Timings vary with JIT/runtime state.

The unchanged five-player synchronization probe remains at **0 measured bytes/tick**
with settings inactive or active. A third scenario now includes 90 occupied slots,
40 talent entries and 30 mystic positions per player; it also stays at 0 measured
bytes/tick. The first two historical workload definitions remain available for
comparison. Allocation checks enforce the existing 1 KiB/tick ceiling and at least
a 25% snapshot reduction; wall-clock timings are informational only.

Paths retained after review:

- Native Mirror collection IL and compiled addon call sites use concrete struct
  enumerators for inventory, talents and mystic positions. `Dictionary.Values`
  caches its first wrapper; it does not allocate one on every tick. These scans
  retain their immediate occupancy and budget safety checks.
- Names cache normalization/status until their inputs change. Localization and
  preset disk I/O occur at initialization or explicit refresh/save boundaries.
  Native UI lookup is a hash lookup, and chat reflection occurs only on rebinding.
- Rabbit sharing allocations happen per qualifying drink, not per frame. Rechecks
  around native callbacks protect death/disconnect/replacement handling and remain.
  Green effects are sent only after actual healing. Low-MP alert reflection is
  event-only; no speculative delegate/pooling rewrite was warranted here.
- Merchant placement is bounded, runs at floor/settings events, and can be the
  largest burst in that feature. Tile/collision searches were not changed without
  live evidence of a stall; placement and chance-roll ordering remain intact.
  The universal damage hook performs an allocation-free ownership lookup.
- Disconnect diagnostics are event-driven. Shared reconciliation retains typed
  observations, immediate boundary flushes and write readback/recovery guards.

Validation includes 782 runtime checks, full/compact snapshot equivalence
through pending joins, faults, native edits, preset refresh, authority/scope changes
and Korean language selection, allocation budgets, portable tests and installed-game
compatibility checks. Debug/Release builds use `-p:DeployMod=false`. Live Unity/Mono
profiling and multiplayer rendering were not performed; no deployment was run.

## Original review: 0.15.2

2026-09-25. Goal: reduce repeated CPU work and allocations while preserving
per-frame synchronization, native-read guards, host authority and command semantics.

## Plan and evidence

- [x] Inspect controller Update/LateUpdate, shared reconciliation, name status,
  resource/stat observations, and settings UI refresh paths.
- [x] Add a reproducible runtime allocation probe before changing production code.
- [x] Cache immutable stat/resource keys; reuse guarded synchronization scratch
  collections; compare typed snapshots in the shared coordinator.
- [x] Replace resource comparison strings and per-frame name diagnostic lists.
- [x] Verify allocation budget plus existing synchronization, reconnect, reset,
  partial-write recovery and native compatibility tests. Review the final diff.
- [x] Record measured results and operational limits; no deployment.

The repository workflow commits and pushes the verified changes together.

The original probe ran real addon synchronization over four game API fixtures, with 100
raw entries and 24 occupied slots per player, after 2,000 warm-up ticks. The active
case enables ten relative stats, Fountain multiplier, choices and three live
resources. It measures 10,000 unchanged ticks using per-thread allocation counters.

Baseline at `b2c6a84` (Release, .NET 10 fixture): no settings 1,425 bytes/tick and
5.33 microseconds/tick; all active 23,440 bytes/tick and 40.28 microseconds/tick.
Elapsed times are informational and machine/runtime dependent, not an FPS claim.

Run from the repository:

```
dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false -- --perf
dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false -- --perf-check
```

As of 0.15.3 the probe uses **five players**, matching the disconnect investigation;
the historical four-player measurements below are unchanged. The allocation
check still allows 1 KiB per unchanged tick and does not assert
wall-clock timing. Unity/Mirror transport and rendering costs require live profiling.

## Changes in 0.15.2

The shared coordinator now offers typed per-subject observation cursors through
`ReconciliationRule<T>.ObserveValue`. Immutable snapshots use typed equality,
avoiding boxing and reflective value-type equality each frame. The object-based
constructor remains compatible. Every cursor still captures all inputs, honors
explicit invalidation, retries readiness waits, and captures the state after a
write to prevent feedback loops.

Resource observations compare the three live resource snapshots and their intent/
availability directly rather than building formatted strings. Stat/resource
ownership-marker strings are initialized once with their immutable definitions.
Resource catalog lookup no longer allocates an interface enumerator. Session
scratch collections are reused under the existing reentrancy guard and cleared
in `finally`, retaining capacity but no departed-avatar references.

Native name synchronization reads one result directly instead of allocating a
diagnostic list every frame. Diagnostic text and profile normalization are cached
until their inputs change. New avatars, profile absence/edits, name acknowledgments,
solo/multiplayer transitions, and timed retries still invalidate the relevant state.

A final lifecycle regression reproduced retention of an avatar that received an
early resource write but disconnected before normal synchronization registered it.
There was then no tracked subject to invoke `Forget`, and the resource dictionaries
kept that avatar alive until scope teardown. Applied/restored observations now use
weak-key tables, as the talent checkpoint lifetime already does. Explicit departure
and scope cleanup remain. Weak-reference tests cover both applied and restored
early joins; the applied-cache regression failed before this fix.

The settings UI already polls only while open, at 0.25-second intervals, and only
recalculates text layout when the rendered text changes. Saved-preset reads are
cached, and chat reflection happens only when binding to a replaced input. These
paths were retained. No synchronization throttling or removal of write validation
was introduced.

## Measured outcome and limits

| Four-player unchanged fixture | Before bytes/tick | After bytes/tick | Before time/tick | After time/tick |
| --- | ---: | ---: | ---: | ---: |
| No settings | 1,425 | 0 | 5.33 us | 4.79 us |
| Ten relative stats and other live settings | 23,440 | 0 | 40.28 us | 14.23 us |

The table records one baseline and the first optimized Release run on the same
.NET 10 fixture. Final verification after weak-key cleanup measured 4.75/18.30 us
per tick, still 0 bytes in both cases. Timings vary; zero means no measured steady allocation in these fixture paths,
not zero allocation for the addon or game. Commands, changed inputs, pending
inheritance, write recovery and open-panel snapshots still allocate intentionally.
Real Mirror collection enumerators, Unity object access, TMP rendering and the
Mono runtime are not simulated. Use the Unity profiler in a four-player run to
establish actual GC/frame-time impact; no FPS improvement is claimed here.

Regression coverage exercises typed observation subject isolation, readiness waits,
post-write capture, duplicate/reentrant invalidation, faults, late registration,
explicit recovery, existing reconnect paths and actual name-controller transitions.
Allocation limits cover shared unchanged observation and acknowledged/waiting names,
in addition to the four-player probe. Native write journals and authority rules
are unchanged.

Final verification: **1,567 checks passed** (946 portable, 502 runtime, 57 starting
hooks, 62 budget/inventory hooks), plus the two four-player allocation budgets.
Debug/Release builds and installed-game IL compatibility passed with deployment
disabled. Independent review found no actionable regression in observation or
name semantics. Live Unity/Mono performance profiling was not performed.
