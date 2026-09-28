# Performance review and optimization

Goal: reduce proven repeated work and stale object retention without weakening
per-frame reconciliation, native-read guards, command validation or recovery.

- [x] Trace controller polling, resource/stat observations, UI/localization/name
  refresh, and Rabbit/Merchant/Choice/diagnostic hooks; measure current fixtures.
- [x] Avoid building unused diagnostic text for non-Status panel pages. Preserve
  all live player values, readiness/permission flags, preset refresh and language
  behavior; default chat/status snapshots remain complete. Verify equivalence
  across the existing UI state-transition cases and measure allocation reduction.
- [x] Release the Choices cleanup journal and dungeon reference after successful
  recovery or host-scope teardown. Preserve failed same-session recovery and test
  that abandoned/recovered objects can be collected.
- [x] Run runtime/performance tests, portable and installed-game compatibility
  checks, and Debug/Release builds with `-p:DeployMod=false`. Review the diff,
  document evidence and limits, then commit and push without deploying.

The existing five-player unchanged fixture measured 0 bytes/tick and about
21 us/tick with all tested settings active. Full status construction measured
103,958 bytes/refresh; the open panel currently requests it four times per second
on every page. Timings are .NET fixture measurements, not Unity FPS measurements.

Native collection IL confirms concrete value-type enumeration for inventory,
talent and mystic-position observations. These loops do not box each frame;
retain their immediate validation. Retain potion callback lifetime checks and
merchant placement/random-roll ordering because they protect gameplay semantics.
