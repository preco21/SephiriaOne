# Match countdown cadence

Implement the requested shared timer policy: initial notice, then one notice
every five elapsed displayed seconds above five remaining, and each displayed
second at five or below. Skip missed intermediate updates after a delayed frame.
No timer, gameplay or respawn changes. No deployment.

Keep the existing Zombie/chaser markers. Decouple Play Tag's one-second floating
timer from its overhead/chat notice. Add Tournament opponent-only floating text
on the existing native cosmetic channel, refreshed once per displayed second;
timer chat follows the shared policy. Native particles drift/fade and cannot be
explicitly recalled on stock clients, so stop new emissions immediately on
death, pairing, connection, readiness or match invalidation. No new RPC protocol.

- [x] Add regression checks for cadence and independent continuous text.
- [x] Share the notice gate; keep reentrancy/receipt cleanup in each mode.
- [x] Update Tournament EN/KO opponent label and native contracts.
- [x] Run runtime/localization/native checks and non-deploying Release build.
- [x] Document evidence/limits, review, commit and push.

Verification: Release 0.106.0 builds with zero warnings/errors; 17,897 runtime
checks plus 69 Bat lifecycle checks pass, as do portable/localization/installed
native contracts and all 41 compiled Harmony API checks. Independent read-only
review found no actionable regression. No deployment or live guest test.
