# PvP debuff schedule protection

**Goal:** Keep an effect's first native immediate hit, then require its configured
interval before another admitted hit, regardless of reapplication or replacement.

**Architecture:** Extend the existing per-attacker/opponent combat window with
independent debuff clocks. Native periodic timers already survive AddStack and
RefreshDurationTimer; leave these, lifetime, charge and stacking mechanics intact.
Add eight independent, default-on settings to PvpDotSettings. Dark Cloud retains
its separate strike clock and does not need a reapplication setting.

**Constraints:** Host-only; PvP balancing only; native PvE unchanged; no deployment.
Live edits must not reset budgets, scores, participants or running modes. Preserve
connection, life, floor and session cleanup through existing combat windows.

## Implementation and verification

- [x] Add regression tests demonstrating repeated periodic damage currently gets
  admitted inside its interval, and commands currently reject preserve controls.
- [x] Extend PvpDotSettings and FriendlyFireCommand with per-effect preserve
  on/off/reset. Default/reset is on; disable independently without changing intervals.
- [x] Extend FriendlyFireDebuffBalance/FriendlyFireBalanceBudget clocks for Burn,
  Plasma Burn, Frostbite, Poison and Wound; retain Lightning, Freeze and Chim.
  Classify Plasma shock by DEBUFF_ELECTRIC before the shared Debuff_Plasma ID.
  Reserve only positive budget-admitted hits; rejected reapplications never move
  the clock. Record admitted hits while the option is off for safe live enabling.
- [x] Add preset v35, scroll-row checkboxes, English/Korean help and status. Old
  presets inherit defaults; malformed/new keys in old versions are rejected.
- [x] Test first hit, rapid reapplication, exact boundary, independent effects,
  stacking/lifetime/Freeze stun, live settings, balance disable, all four modes,
  reconnect/revive/epoch resets, PvE, nested hits and warmed-path allocation.
- [x] Verify installed native timer contracts, Release build, combat/runtime/
  policy/panel tests with DeployMod=false. Update feature findings and version.
- [x] Obtain a fresh code review, address findings, conventional commit and push.

**Review focus:** Default-on migration; native ID classification; persistent
damage clocks across replacing effects; mode/connection cleanup; preserving
native stacks, expiry attacks and stun; no allocation or polling per frame.

**Native evidence:** Assembly-CSharp MVID ccf97024-28bd-48f1-9666-c768c2bd4b2b.
CharacterDebuff.AddStack refreshes lifetime and stacks. Base RefreshDurationTimer
resets only buffTimer. Electric/Plasma refresh may invoke shock damage; Freeze
application may invoke damage and stun. Initial creation uses AddStackInner.
No extra timer restoration hooks are needed for the audited five periodic types.


## Completion evidence

0.69.0 implementation uses the existing damage clocks; no new timer hooks or
per-frame work. Release build (DeployMod=false) has zero warnings/errors.
Combat: 6,120 checks; runtime: 3,387 plus 69 Bat checks; schedule policy: 742;
interval policy: 1,708; catalogs: 3,945; panel/native contracts and 33 HarmonyX
API references passed. Fresh review found a precision defect when native timer
progress and absolute timestamps round differently. An accumulated-frame test
failed before the bounded precision fix and passed afterward (60/120/144 FPS,
multiple uptimes/intervals); same-frame repeats remain blocked. No other material
review findings. No deployment or live multiplayer verification.
