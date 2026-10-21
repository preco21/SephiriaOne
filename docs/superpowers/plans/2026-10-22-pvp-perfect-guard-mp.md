# PvP Perfect Guard MP multiplier implementation plan

**Goal:** Add an optional live Perfect Guard MP-cost multiplier using the shared
PvP policy, command/UI, mode and connection lifetimes, with native PvE behavior.

**Design:** Record the native successful Perfect Guard branch in the current
nested hit context, then adapt its final MP cost after native modifiers. Ordinary
guards, parries and automatic shields remain on their existing paths. The
enabled multiplier is explicitly exempt from the strict shield MP minimum,
per the user's follow-up. It multiplies the native integer cost and rounds up;
x1 keeps native cost. Disabling it restores existing balanced guard rules.

**Defaults:** independent toggle off, retained multiplier x2; accepted range
x1..x100 with two decimal places. UI may accept `2` or `x2`. Setting a multiplier
does not implicitly toggle the option, interrupt modes, reset KDA or budgets.

## Interfaces and ownership

- Policy: new default-safe `PvpPerfectGuardSettings(bool enabled = false,
  decimal multiplier = 2m)` with Enabled, Multiplier, Scale (double), Number,
  HasChanges; `FriendlyFireSettings.PerfectGuard` and final optional constructor
  parameter `perfectGuard = default`. Typed nullable `PvpPerfectGuardChange`
  holds enabled/multiplier/reset; preserve all siblings and WithEnabled overlays.
- Commands: `/one friendlyfire perfect-guard on|off|reset`; `perfect-guard
  multiplier xN|N|reset`. Family reset restores off/x2; value reset restores x2
  only. Existing `/one save` persists nondefaults in v45 rows `friendlyfire
  perfect-guard 1` and `friendlyfire perfect-guard-multiplier N`. Old files
  inherit off/x2; reject malformed, duplicate or out-of-version rows atomically.
- Runtime: `MarkPerfectGuard()` only in the audited native half-cost branch;
  hit-context flag restores through existing nested/finalizer lifetime. Compute
  integer-native MP cost after modifiers, then configured multiplication with
  finite/overflow/available-MP guards. Reuse stock UseMp/Networkmp accounting;
  no stat writes, retained actor collections, polling or per-hit allocations.
- UI: PvP Balance left column retains master/rapid-page link in its top row,
  shield minimum row34, Perfect Guard toggle row70 and multiplier row106,
  existing amplification rows145+. Refill remains on Rapid Hits. Existing drafts,
  availability, translation and shared Execute apply/reset semantics.

## Tasks

- [x] Add policy/parser/preset/session tests, then implement settings and v45.
  Verify bounds, default(T), toggle/value reset, inactive configuration, copies,
  all mode overlays, saved reload and unchanged revisions/KDA/match tokens.
- [x] Add executable guard regressions, then extend the exact native guard
  rewrite and MP arithmetic. Cover x1/x2/x3/fractional/max, guard reductions and
  artifact callbacks, floor interaction, huge/empty MP, live disable, nested hits,
  exceptions, source receipt/mode changes, PvE and nonperfect defenses.
- [x] Add UI and EN/KO, updating native route/draft/refill/guard contracts.
  Make cost basis and minimum exemption explicit; preserve numeric drafts.
- [x] Build Release with `-p:DeployMod=false`; run combat/runtime/panel/catalog
  and installed-game compatibility checks; request independent review.
- [x] Record findings and bump version to 0.84.0.

Delivery follows the repository's conventional commit/push workflow after
verification; no deployment. Final checks passed: 9,001 combat, 13,931 runtime,
212 Perfect Guard policy and 4,448 catalog assertions, plus panel/localization
and installed-game contracts. Review covered the user's final floor exemption.

## Native evidence

REA identity `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`:
installed SHA256 c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510,
MVID ccf97024-28bd-48f1-9666-c768c2bd4b2b. Matching cached ILSpy source shows
10 MP base → GuardResist → native perfect-guard x0.5 → callbacks → int conversion
→ UseMp → native guard break. Identity inspection alone does not prove execution;
the exact branch is verified by executable fixtures and installed IL contracts.
Private source and binaries stay outside Git. No deployment or live-game launch.
