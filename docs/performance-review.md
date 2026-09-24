# Synchronization performance review

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

The probe runs real addon synchronization over four game API fixtures, with 100
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

The allocation check allows 1 KiB per unchanged four-player tick; it does not assert
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
