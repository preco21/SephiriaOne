# Mystic Jar spawn-rate implementation plan

Add a host-controlled override for normal random MysticPot placements. Preserve
native chapter gates and hidden-room reward selection. Default/reset uses each
placement's native chance. Exact percentages accept 0..100; xN uses each native
placement's chance, clamped to 100%. x1 restores native. Existing generated
objects are not rerolled or replenished.

- [x] Add failing shared-command and executable native-hook fixtures.
- [x] Implement a bounded settings value and `/one jars chance N|xN`, status,
  help and reset/off commands through SessionSettings.
- [x] Replace only the appearRate read inside MysticPot.OnStartServer; retain
  the seeded RNG, chapter test and native visibility SyncVar write. Scope out
  synchronous hidden-room rewards with exception-safe context restoration.
- [x] Add v13 presets, snapshots, EN/KO messages and a Mystic Jar panel page.
  Wrap tabs to two rows to preserve usable labels as the panel grows.
- [x] Verify native IL, exact 0/100 boundaries, native per-location multipliers,
  resets, late joins, run/session changes and lifecycle cleanup. Review and
  document limitations and build. No deployment.

The user confirmed the normal-random-only scope. Debug/Release builds completed
without warnings. Release fixtures passed 139 jar-hook checks, 884 runtime checks,
48 localization checks and 32 friendly-fire regression checks. The full portable
and installed-game compatibility suite passed, including 55 jar policy/preset
checks and 1237 bundled-catalog checks. Independent review found no actionable
issues. Live Unity and multiplayer testing remains outstanding. Repository workflow
requires a conventional commit and push after these checks.
