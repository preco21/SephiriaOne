# Rabbit PvP shared-healing toggle

Add a host-only `block-pvp-share on|off` Rabbit option, default on, exposed in
the existing Rabbit tab and shared command dispatcher. Turning it off bypasses
only the PvP participation restriction; Share must still be on and potion,
costume, distance, floor and connection guards stay intact. Re-read the toggle
before each recipient and after heal callbacks. Do not touch White Bread or HP
Steal rules. Reset and older presets preserve the current restriction.

- [x] Add command/default/preset and live healing regressions, observe failure.
- [x] Extend Rabbit settings, UI, status, tooltip/role notices and EN/KO text.
- [x] Persist opt-out as version 33 without changing older preset defaults.
- [x] Verify potion/description/runtime/localization/native tests and non-deploy build.
- [x] Update docs/version and complete independent review. Never deploy.

Review corrections: friendly-fire help now explains the optional restriction;
command and shared checkbox recovery permit restoring on when hooks fail without
resetting unrelated Rabbit settings. Existing checkbox recovery defaults remain
off. Regression tests reproduced the unavailable-hook failure before correction.

All listed suites passed; final review found no further actionable issues.
Conventional commit/push follows verification under the standing repository
workflow. No live multiplayer/UI validation or deployment was performed.
