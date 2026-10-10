# Synchronization performance review

## Follow-up: 0.42.0 panel snapshots (2026-10-10)

Reviewed the current periodic controllers, shared synchronization, deathmatch and
companion guards, and panel snapshot consumers. Baseline was `c186f34`; the
[plan](plans/2026-10-10-panel-snapshot-performance.md) records the scope. The
unchanged synchronization workloads already allocate zero in the fixture. Native
ownership/readiness checks, command validation, boundary flushes, combat rules,
deathmatch cadence and merchant randomness remain unchanged.

Every non-Status tab still captured all 27 player stats, extra choices and five
resource descriptions four times per second. That included inventory/talent
scans and formatting active/saved settings even on tabs displaying only toggles.
Snapshots now accept explicit `SnapshotContent` sections. The panel requests
Stats, Choices, Resources or Presets for those tabs, and base records for Fountain
and feature-only tabs. Status and unknown future tabs request everything.

Every capture still reads current session/run identity, ready-player membership,
permissions, faults, compatibility flags, feature options and preset validity.
The player records retain IDs/names and Fountain totals; omitted dictionaries
are immutable empty views. Selected values are read afresh on every capture.
Diagnostics imply all sections. The original full/compact API and chat output
remain supported, and preset refresh/invalidation behavior is unchanged. No
cross-frame gameplay or text cache, slower polling or new dependencies were added.
The fixed choice-name array is also shared instead of allocated per player.

Release .NET 10 fixture, five players with all 27 relative stats and existing
Fountain/Choices/resource settings active:

| Snapshot used by tab | Before | After | Allocation reduction |
| --- | ---: | ---: | ---: |
| Fountain, Rabbit, Merchant, Items, Combat, Spawns, Costumes, Updates, Deathmatch | 25,432 B | 1,760 B | 93.1% |
| Stats | 25,432 B | 8,000 B | 68.5% |
| Choices | 25,432 B | 3,040 B | 88.0% |
| Resources | 25,432 B | 11,200 B | 56.0% |
| Presets | 25,432 B | 8,232 B | 67.6% |
| Full Status report | 116,558 B | 116,318 B | 0.2% |

Amounts are per refresh. Feature-only tabs avoid about 95 KB/second of managed
allocation at the existing four refreshes per second. All three unchanged-sync
workloads still measure **0 B/tick**. Legacy captures of all values without
diagnostics measure 25,192 B/refresh. Sample capture times were 1.45 microseconds
for base records and 2.37–14.86 for selected sections, versus 31.59 for the old
non-Status path; timings vary with JIT/runtime state and are not asserted.

The new base-record allocation budget first failed with the old capture behavior
(25,432 B against a 4 KiB ceiling) and passes with section selection. Regression
checks compare selected sections, common fields and full diagnostics throughout
the existing pending-join, fault recovery, native-edit, saved-file, host-replacement
and EN/KO scenarios. Additional checks cover duplicate roster entries, immutable
earlier snapshots and empty dictionaries, no writes or revision changes, teardown,
and the actual compiled panel's tab-to-section selection and refresh entry points.

Validation: 1,373 shared runtime checks plus 69 Bat lifecycle checks; all allocation
budgets; 22 panel geometry/checkbox/lifetime checks; the portable and installed-game
compatibility suite, including 3,066 localization checks; Debug/Release builds
with zero warnings/errors. All used `-p:DeployMod=false`. No deployment, game
launch, live Unity/Mono profiling or guest rendering test was performed. These
figures measure fixture allocations, not whole-game FPS or network performance.

## Follow-up: 0.36.1 (2026-10-09)

Reviewed synchronization and status capture after expanding the stat catalog to
27 entries, plus panel refresh, native damage, costume grants, item restrictions
and event/merchant spawn entry points. Baseline was `df0b920` (0.36.0). The
[plan](plans/2026-10-09-performance-review.md) records scope and validation.

The full Status report allocated temporary lists and status objects for each
player/stat reconciliation result. It also assembled many intermediate strings
for rule descriptions and the 27-stat player rows. These costs scale with the
number of players and enabled relative settings, even when values stay unchanged.

- Reconciliation statuses are now immutable values. `AppendStatuses` copies
  already-recorded outcomes into a caller-owned list. It does not enroll subjects,
  observe native inputs, apply rules, consume invalidations or advance revisions.
  `Describe` still returns an independent snapshot for existing callers.
- Status and disconnect diagnostics reuse one status list and text builder
  **within each report**. Player stat rows are built directly in another local
  builder. No mutable buffers or rendered text are cached across captures.
- Text, ordering, current EN/KO labels, historical fault details and number
  formatting remain unchanged. Captures still read current native values and
  return detached, read-only player snapshots.

Release .NET 10 fixture, five players with all 27 relative stats and existing
Fountain/Choices/resource settings active:

| Path | Before | After |
| --- | ---: | ---: |
| Full Status snapshot | 195,602 B/refresh | 116,558 B/refresh |
| Non-Status snapshot | about 25,433 B/refresh | about 25,433 B/refresh |
| Unchanged synchronization, all three workloads | 0 B/tick | 0 B/tick |

The full report reduction is **79,044 bytes (40.4%) per refresh**, or about
316 KB/second at the existing four refreshes per second while Status is open.
This is managed allocation avoided, not an FPS or whole-game memory claim.
Full-report sample time changed from 176.94 to 150.52 microseconds; timings vary
with JIT/runtime state and are not asserted. The new 160 KiB full-report ceiling
failed on the original implementation and passes after optimization. Existing
compact-report and unchanged-sync budgets remain intact.

Preserved after review: per-frame native observations and critical boundary
flushes, write/readback/recovery checks, native baseline arithmetic, gameplay
messages, inventory/talent safety scans, merchant schedule/placement order and
costume grant timing. Spawn and costume operations remain event-driven. No new
polling, network writes, persistent caches, dependencies or assets were added.

Regression coverage includes exact stat/rule output, buffered read order and all
reconciliation states, pending and unknown subjects, late rule registration,
revisions, re-entry, forgetting/teardown, immutable earlier snapshots, and current
EN/KO native-fallback diagnostics. Runtime fixtures also retain their existing
restart, native-edit, authority, preset and partial-write recovery coverage.
No deployment or live Unity/Mono/multiplayer performance test was performed.

## Follow-up: 0.26.1 (2026-09-30)

Reviewed the current session/stat/resource reconciliation, names, panel/chat and
localization, Rabbit/Choices/Merchant hooks, disconnect diagnostics and cleanup.
The [plan](plans/2026-09-30-performance-review.md) records this pass. Baseline was
`994635d` (0.26.0); no deployment or live Unity profiling was performed.

Three concrete sources of avoidable work were fixed:

- **Merchant placement logging:** installed `Safe.Find` logs every nearby safe
  while finding the nearest. A crowded room can invoke it 128 times per type per
  search. Placement now reads the current native safe registry once and performs
  a silent presence check with early exit. It retains Unity alive checks, the
  exact inclusive 3D distance test, candidate order and occupancy checks. It does
  not cache safes across searches or sessions. A missing registry fails closed;
  initialization/list replacement recovers on the next search. Final nearest-safe
  ownership checks and native `SetSocialID` remain unchanged. The room regression
  first failed with 129 native lookups (one accepted point, 128 rejected points);
  the new path makes zero such calls or logs. This avoids the candidate-loop log
  burst; it does not suppress the game's normal per-merchant initialization log.
- **Panel snapshot copies:** each capture built three fresh dictionaries per
  player, then copied them again. An explicit ownership-transfer factory wraps
  those private capture dictionaries directly. The ordinary constructor still
  makes defensive copies. Every refresh reads fresh native state; previous
  snapshots and their read-only collections remain detached and immutable.
- **Pending rich-text names:** preserved foreign hex colors and `noparse` markup
  were parsed again on every waiting publication tick. A bounded per-instance
  input/result cache now avoids that repeated normalization and clears on Reset.
  Profile edits, retry timing/exhaustion, acknowledgments and solo restoration
  retain their original behavior.

Release .NET 10 fixture measurements:

| Path | Before allocation | After allocation |
| --- | ---: | ---: |
| Five-player full snapshot | 113,038 B/refresh | 107,678 B/refresh |
| Five-player non-Status snapshot | 28,521 B/refresh | 23,160 B/refresh |
| Pending name: foreign single-character hex color | 664 B/tick | 0 B/tick |
| Pending name: foreign hex wrapper | 224 B/tick | 0 B/tick |
| Pending name: `noparse` with hex markup | 272 B/tick | 0 B/tick |

Compact snapshot allocations drop **18.8%**, saving approximately 21.4 KB/second
at four refreshes per second while the panel is open. Full snapshots save 5,360
bytes per refresh. All three five-player unchanged synchronization scenarios
(inactive, active, active with populated collections) still measure **0 B/tick**.
Final sample timings were 5.57/18.31/6.24 microseconds per synchronization tick and
74.54/23.07 microseconds for full/compact snapshots. These are fixture timings,
not Unity frame-rate claims; JIT, native engine work and GC pauses are not modeled.

Kept unchanged after review: per-frame readiness/native-input observations,
precommit/readback/recovery validation, inventory/talent occupancy scans, re-entry
and death guards, Rabbit healing/FX/MP notifications, Choices cleanup, merchant
chance/reservation ordering and once-at-spawn floor HP. Native collection scans
use concrete struct enumerators. Additional merchant key/array allocations and
complete route-history scans occur only at floor/settings events; they were left
intact rather than adding caches that need new lifecycle invalidation. Panel
text formatting is still bounded to open-panel refreshes; localization/preset I/O
and chat reflection retain their existing event-only boundaries.

Verification passes: 823 shared runtime checks, 551 merchant checks in 75
scenarios, 63 placement checks, the full portable suite including 48 name retry
checks, allocation budgets and installed-game compatibility. The placement tests
cover radius boundaries, vertical distance, destroyed/null safes, native registry
replacement and loss, new stock, deterministic variants and crowded rooms.
Both production build configurations pass with zero warnings/errors and
`-p:DeployMod=false`. Test realistic multiplayer sessions with the Unity profiler
before attributing any live FPS or disconnect improvement to these changes.

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
