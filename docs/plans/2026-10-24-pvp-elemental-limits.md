# PvP elemental limits implementation plan

User-approved scope: four independently configurable soft/hard pairs, one
default-enabled elemental-limit toggle, default 50/100, same curve as All Damage
Amplified. Prefer preserving simultaneous PvE damage; user permits participant
stat limits during PvP when exact target-specific reconstruction is too complex.

The inspected native damage packets already contain elemental contributions.
Reconstructing every producer would require source-specific formula and lifetime
tracking. Use the permitted effective participant-stat fallback, without writing
raw/saved stats. Monster damage from participants is affected during this scope.
Native mutation/derivation readers must retain uncapped values. Existing spawned
attacks retain their native creation-time damage snapshots.

## Units and interfaces

- Extract `PvpStatLimit` from `PvpAmplificationSettings`: default soft 50/hard 100,
  cached reciprocal, `Limit(float)`, `Soft`, `Hard`, number strings, validation.
  Amplification delegates to this curve and retains all existing semantics.
- `PvpElement` order: Physical=0, Flame=1, Ice=2, Lightning=3, matching the native
  converted-element index. `PvpElementalSettings` defaults enabled, stores four
  limits, exposes `GetLimit(PvpElement)`, `Enabled`, `HasChanges`.
- Extend `FriendlyFireSettings.Elements` and command/preset paths. Commands:
  `/one friendlyfire elements on|off|reset` and
  `/one friendlyfire elements physical|flame|ice|lightning limits S H`.
  Each pair is atomic. Support per-element reset. Live edits retain match/KDA/
  damage-budget state; mode overlays copy settings unchanged.
- Extend the existing PvP Balance scroll area with toggle and four paired rows,
  English/Korean text and shared command dispatch. Save changed settings in v52.
- Hook the final converted-element read with shared live participation and
  balancing checks. Round the resulting stat down at the native integer boundary.
  Protect native persistent/stat-derivation readers using exception-safe scopes;
  validate installed contracts before installing hooks. No new scene scans,
  permanent stat edits or restore ledger; reuse existing participant admission.

## Verification sequence

1. Write failing curve/default/parser/preset tests, implement shared policy and
   settings plumbing, verify old amplification tests and new independent pairs.
2. Write failing executable hook tests for enabled/disabled, native penalties,
   independent elements, live edits, selection/mode changes, reconnect, nested
   bypass and exceptions. Implement the stat overlay and audited bypasses.
3. Validate native getter/conversion/derivation anchors, shared hook ownership
   and rollback. Confirm permanent values and native conversion order are intact.
4. Run non-deploying Release build, portable/native compatibility, runtime,
   combat, localization and panel suites. Measure warmed stat-hook allocations.
5. Independent code review, document limitations and commands, bump release,
   conventional commit and push. Do not deploy.
