# Rabbit potion balance implementation plan

**Goal:** Add independent, default-off Wing-Eared Rabbit HP-potion MP cost and
Survival random-stat suppression; preserve other costumes, potions and passives.

**Architecture:** Extend existing Rabbit session intent and controls. Guard the
completed native HP-potion operation on the host, and suppress only the native
Survival passive callback within that validated operation. Never remove the
general potion-drink event or modify talent allocations. Native HP/MP/inventory
state continues reaching unmodified guests.

## Constraints and design

- Work in `C:/Users/preco/repos/SephiriaOne`, main, starting at f8d0426.
- All build/run commands MUST pass `-p:DeployMod=false`; never deploy.
- Main handles final conventional commit/push, not delegated workers.
- Reuse current commands/panel/presets/read-only notifications. No new hotkeys.
- All four options are independent and default off. Old two-flag settings remain
  compatible. Reset clears all four; saves preserve all four; current intent is
  read on use, so reconnects/restarts do not replay healing or MP debits.
- Shared settings: `RabbitPotionSettings(bool infinite, bool share,
  bool consumeMp = false, bool suppressSurvival = false)`; new bool properties
  `ConsumeMp`, `SuppressSurvival`; `public const int MpCostPerDrink = 10`.
- Commands `/one rabbit mp-cost on|off`, `/one rabbit suppress-survival on|off`;
  existing infinite/share/status/reset/save commands keep working.
- New preset v5 rows `rabbit mp-cost 1`, `rabbit suppress-survival 1`; canonical
  0/1 input only. v1-v4 still load. v4 accepts only its original rabbit options;
  v5 accepts all previous rows. Write v5 only when a new option is enabled.
- Scope to live server-owned, alive, ready HolyRabbit and the existing HP-only
  allowlist IDs 0/1/37 with exact PotionEffect_Regeneration. No mana/status/buff
  potion gets infinite uses, MP cost, shared healing or Survival suppression.
- Initial cost is 10 MP at completed drink; insufficient MP blocks the entire
  drink (no HP, sharing, potion events, stat gain or quantity loss). Cancelled
  drinks cost nothing. Preserve native animation/wield cleanup on rejection.
  Avoid a prefix solely skipping WieldingPotion.Drink: its controller invokes
  OnDrinkPotionServerside afterward even if Drink was skipped.
- Native MP debit must use native synchronized state. Revalidate lifetime and
  funds at completion, do not debit after unrelated scene/connection/costume
  changes. Contain failures without replay or overwriting unrelated MP changes.
  Inspect native UseMp callback/INFINITYMP behavior before choosing the debit
  mechanism; document any native immunity semantics retained.
- Suppress only PassiveObject_PotionAndRandomStat.HandleDrinkPotion(PotionEffect)
  when its player/effect match the current eligible scoped Rabbit drink and the
  option is enabled. All other potion callbacks stay intact. No retroactive
  removal of already-earned stats; no global talent disable/unsubscribe.
- Description adds enabled MP-cost and suppression lines on host only, preserving
  native text. Guest descriptions stay native, gameplay remains host controlled.

## Task 1: Shared settings and UI (delegated)

Own RabbitCommand.cs, RabbitPotionSettings.cs, SessionPreset.cs, SessionRabbit.cs,
SettingsPanelRabbit.cs, RabbitDescriptionText.cs, and the corresponding pure,
runtime and description test files. Update test fixture settings shapes where
needed, but coordinate with native task before touching potion-hook Stubs.cs.

- [x] Add failing tests for parser, independent flags, reset, old preset
  compatibility, v5 strict atomic parsing and round trip.
- [x] Implement shared settings/commands/presets/status.
- [x] Add four independent rows to Rabbit panel within existing dimensions;
  update help/readout and dynamic description. Off/reset stays available under
  compatibility failure. Add focused description/runtime tests.
- [x] Run relevant suites with deployment disabled and self-review.

## Task 2: Native MP guard and Survival suppression (delegated)

Own RabbitPotionNativeHooks.cs and new RabbitPotionNative*.cs partial helpers,
RabbitPotionFeature.cs only if needed, tests/SephiriaOne.RabbitPotionHookTests,
and GameRabbitCompatibilityTests.cs (coordinate last edits with main).

- [x] Verify native passive callback, HP-only classifier, UseMp/MP replication
  and controller cleanup. Native sources live in `%TEMP%/sephiria-healing-audit`
  and `%TEMP%/sephiria-mod-research/game-types`. ILSpy is at
  `%TEMP%/sephiria-mod-research/ilspycmd/tools/net8.0/any/ilspycmd.dll`.
- [x] Design smallest safe completion guard/debit around native controller
  operation and scoped Drink. Keep core hooks readable by extracting MP and
  Survival boundaries into focused partial files if needed.
- [x] Write failing real-Harmony tests for MP success, insufficient/cancelled
  drink with intact cleanup, precise Survival suppression and unrelated events.
- [x] Implement compatibility-guarded hooks, preserve host/guest and lifetime
  checks. MP option should not silently grant free healing on failed debit.
- [x] Test all four option combinations as relevant, non-Rabbit players, mana
  and non-HP potions, nested calls/other player's callback, normal Survival when
  off, callback exceptions, reset/unload, native costume/session changes and
  live guest ownership. Ensure no unrelated callback is suppressed.
- [x] Extend installed-game IL/signature tests for new boundaries; test Harmony
  fixture and installed-game build with deployment disabled. Report actual native
  ownership/consumer contracts and remaining live test limitations.

## Task 3: Integration, documentation and review (main)

- [x] Record native asset evidence identifying Survival rank-5 passive prefab.
- [x] Check delegated work, run existing affected suites and Debug/Release builds,
  installed-game contracts and runtime performance check; review complete diff.
- [x] Update feature guide/history/version to 0.17.0, record verification.
- [x] Fix important review findings, commit and push. Do not deploy.

Scope was explicitly authorized by the user. Initial-cost preference was asked
as optional clarification; use 10 MP unless the user answers otherwise. The
precise Survival hook is preferred over cancelling all drink events or removing
the passive: those alternatives would also disable unrelated native effects.
