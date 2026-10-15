# Play Tag chaser multipliers

**Goal:** Give the current chaser optional, configurable PvP advantages without
native stat writes or changes to other combat modes/PvE.

**Confirmed defaults:** Separate toggles, both on; refill multiplier 2; incoming
damage 75%. Existing bomb rules are already shipped and remain unchanged.

**Architecture:** A pure `PlayTagChaserSettings` value belongs to shared session
policy. `CombatMatchRuntime` resolves the live carrier using current phase,
receipt, scope and bomb state. The existing resolved-PvP-hit boundary composes
the modifiers; there are no per-player applied stat bonuses to restore.

## Configuration/controls (independent implementation task)

- [x] Create `Features/Combat/PlayTag/PlayTagChaserSettings.cs` with default-safe
  readonly value semantics, `RefillEnabled`, `RefillMultiplier` (decimal),
  `RefillScale` (double), `IncomingEnabled`, `IncomingPercent` (decimal),
  `IncomingScale` (double), `RefillNumber`, `IncomingNumber`, `HasChanges`.
  Constructor `(bool refillEnabled, decimal refillMultiplier, bool incomingEnabled,
  decimal incomingPercent)`. `WithRefill(bool? enabled = null, decimal? multiplier = null)`
  and `WithIncoming(bool? enabled = null, decimal? percent = null)` preserve the
  other fields. `ValidRefillMultiplier`: 0.1..10 with at most two decimal places;
  `ValidIncomingPercent`: 0..100 with at most two decimal places. Default(T) is
  both enabled, x2, 75%. Disabled settings retain configured numbers.
- [x] Add `SessionPolicy.PlayTagChaser`, `RecordPlayTagChaser(settings)`, reset and
  HasChanges. `SessionSettings.PlayTagChaser` and `TrySetPlayTagChaser(settings,
  out string)` use existing host/scope and presentation notifications. Edits
  apply live; call `CombatMatchRuntime.ChaserSettingsChanged()` only on actual
  value change. Do not reset combat budgets, KDA or match timers.
- [x] Extend `/one tag chaser-refill on|off|xN|N|reset` and
  `/one tag chaser-damage on|off|N%|N|reset`. Reset restores that option's enabled
  state and default value. Add status/help and EN/KO translations. Add separate
  toggles and numeric Apply controls in the existing 273x162 left editor; retain
  duration, Start/Stop, Reset KDA and shared participant list without overlap.
- [x] Preset v26 records nondefault `tag chaser-refill-enabled 0`,
  `tag chaser-refill N`, `tag chaser-damage-enabled 0`, `tag chaser-damage N`.
  Reject duplicate/invalid/unsupported rows; older presets use both-on defaults.
  Preserve lowest compatible version when no nondefault chaser values remain.
- [x] Add failing-then-passing policy, parsing, saved preset, controls and catalog
  tests in existing portable/runtime runners. Do not edit combat runtime files.

## Shared live combat (parent implementation)

- [x] Add current-role query `CombatMatchRuntime.IsActiveChaser(PlayerAvatar)`.
  Reject idle/countdown/expired/resolved/unarmed/dead/stale/non-enrolled subjects.
  Require actual successful revival before a transferred holder gains bonuses.
  Ordinary current-holder death removes bonuses until alive again.
- [x] Add `CombatMatchRuntime.ChaserRevision` and `ChaserSettingsChanged()`.
  Advance revision on start/transfer/clear/configuration change. This identifies
  hidden A->B->A role changes without resetting accumulated tokens or KDA.
- [x] In `FriendlyFireRuntime.Scale`, use global refill times the enabled
  multiplier only for a current player attacker/chaser; shared companion,
  reflection and debuff attribution retains existing owner rules. Refill remains
  part of the PvP balance toggle. Multiply resolved incoming player damage after
  native defenses, global damage scale, per-hit softening and budget reservation.
  Incoming reduction operates independently of the PvP balancing preference.
- [x] Keep budget currency in pre-incoming-multiplier damage units. Capture the
  hit's incoming factor and divide actual accounted native damage by that factor
  before refunding unused reservations. Zero incoming scale does not reserve
  tokens or pulse clocks. This preserves the reduction even for saturated rapid
  attacks and still refunds genuine MP rounding, armor, veto and overkill losses.
- [x] When a pair budget observes changed chaser revision, credit elapsed time
  at no more than the global base refill (also respecting old/new rate minimum),
  then retain its current effective rate. Never reset tokens or grant another
  burst at transfer/toggle/edit; this is intentionally conservative across changes.
- [x] Test exact global10->20 refill, role handoff/stop/reconnect, normal modes,
  balance-off incoming reduction, shields/refunds/rapid hits, reflection/companion
  paths, nested callbacks and zero-allocation steady state. Native contract tests
  verify the actual runtime modifier calls are wired into shared damage hooks.

## Integration and delivery

- [x] Independent review of both tasks; fix concrete findings with regressions.
- [x] Nondeploy Release build, portable/native compatibility, runtime and combat
  suites, and relevant allocation checks. Preserve original Play Tag policy tests.
- [x] Document configuration, arithmetic, limitations and findings in docs/*;
  increment addon to 0.57.0, conventional commit and push main. No game launch or
  deployment. Automated tests are not live guest-playback evidence.
