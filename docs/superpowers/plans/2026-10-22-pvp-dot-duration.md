# Configurable PvP DoT lifetime

**Goal:** Replace the existing two-second Burn-family cap with a live shared
control defaulting to 1.2 seconds, without extending its coverage to other effects.

**Design:** Keep the existing origin receipt/refresh/destruction path for Burn,
Plasma and their blue/evolved forms. Read a default-safe integer-hundredths scalar
from `FriendlyFireSettings`; no new per-effect cache, polling, scans or timers.
Compare current time with application time plus configured duration to avoid
subtraction precision delaying decimal boundaries. Lower live values expire old
effects on their next native update; higher values cannot resurrect ended effects.
Reapplications still refresh duration; shorter native lifetimes remain shorter.

**Interface:** `DotMaxDurationSeconds` decimal, `DotMaxDuration` float,
`DotMaxDurationNumber`, `HasDotDurationChanges`, default 1.2, range 0.05..10 with
two decimals. Command `/one friendlyfire dot-duration N|reset`. Preset v46 stores
`friendlyfire dot-duration N`; legacy/omitted rows inherit 1.2. Invalid/duplicate
rows reject atomically. Edits preserve interval/schedule settings, KDA, modes,
participants, damage budgets and revisions. Only the shared host policy persists.

**UI:** Add a duration label/input/Apply/Reset plus Burn/Plasma/Blue Flame hint
at the top of the existing right-hand scroll content in PvP Balance. Reuse draft,
availability, Execute and locale refresh lifetimes; retain the full viewport.
EN/KO labels/help/status explicitly identify the currently capped family.

## Implementation and verification

- [x] Add a failing command/default-duration regression, then implement the
  scalar, parser, copies/validation, v46 persistence and live runtime read in
  `FriendlyFireSettings`, `SessionCombat`, `SessionPreset`, `FriendlyFireBurnDuration`.
- [x] Cover defaults, reset, malformed values, copies, older presets, all-mode
  session edits, live lower/raise, refresh, ended effects, native shorter expiry,
  PvE/guest/receipt exclusions and decimal deadlines in the existing test projects.
- [x] Add the shared command-backed UI row in `SettingsPanelPvpDots`, retire
  its draft in `SettingsPanel`, and update catalogs/native UI contracts.
- [x] Run Release build with `-p:DeployMod=false`, combat/runtime/policy/panel/
  localization and installed-game contracts, then independent code review.
- [x] Update shared findings and version to 0.85.0.

Delivery uses the authorized conventional commit/push workflow after checks.
Observed parser and 1.2-second expiry regressions fail before implementation.
Final checks: Release zero warnings/errors; 9,013 combat, 13,973 runtime,
156 duration-policy and 4,466 catalog assertions, plus UI/localization and
installed-game contracts passed. Independent review found no actionable issues.
Old two-second cadence/lifecycle tests now configure two seconds explicitly;
new tests cover 1.2-second/default and custom/live duration boundaries.

Use the repository's current checkout and authorized commit/push workflow.
No deployment or live game launch. Native hooks and coverage do not change;
installed-game contracts validate the existing Burn/Plasma variants and expiry.
