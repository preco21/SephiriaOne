# Performance review after merchant floor scaling

Review the current addon without weakening host authority, immediate native-read
freshness, death/rejoin guards or merchant placement and reservation semantics.
The five-player Release fixture at 994635d measures zero unchanged-tick allocations
in all three scenarios; full/compact snapshots allocate 113,038/28,521 bytes.

Focused fixes justified by source and reproducible tests:

1. Merchant placement calls native `Safe.Find` up to 128 times per room/type.
   That method scans all live safes and logs each nearby one. Read its current
   private safe list once per placement search, validate the field shape, and use
   a silent, allocation-free presence scan with the same alive/distance rules.
   Keep final nearest-safe ownership checks and native initialization unchanged.
2. Fresh per-player panel dictionaries are immediately copied again by snapshot
   constructors. Transfer those private fresh dictionaries to read-only wrappers
   through an explicit ownership boundary, retaining defensive copies for callers.
3. Waiting name publication repeatedly normalizes unchanged noncanonical markup.
   Cache one input/result per name state and clear it on Reset. Preserve all retry,
   acknowledgment, profile-edit and restoration behavior.

Use behavioral/allocation regressions before each change, measure fixture savings,
review independent patches, and run relevant suites/Debug+Release builds with
`-p:DeployMod=false`. Record what was left unchanged and live-profiling limits.
Commit and push verified changes; never deploy during this task.

## Verification

Completed the three focused changes. Red tests reproduced 129 native safe lookups
in the small-room fixture, redundant snapshot allocation above a 25 KiB compact
budget, and 224–664 B/tick name normalization. Green results: zero native lookup
calls in placement, 23,160 B/compact refresh (18.8% reduction), and zero measured
waiting-name allocation. All five-player unchanged tick scenarios remain zero.

823 shared runtime, 551 merchant, 63 room-placement and the full portable suite
pass, including installed-game compatibility. Debug and Release builds have zero
warnings/errors with deployment disabled. See the performance guide for measurement
scope, retained paths, and live-profiler limitations.
Independent read-only review found no actionable correctness or lifecycle issues.
