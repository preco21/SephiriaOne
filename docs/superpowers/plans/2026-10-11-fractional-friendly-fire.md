# Fractional friendly-fire damage implementation plan

**Goal:** Accept 0–300% in 0.01% increments, including 0.1–0.9%, throughout
commands, panel, status, saved presets, deathmatch and all combat effects.

**Architecture:** Store exact integer hundredths relative to the default 100%.
Expose decimal percent for editing/serialization and a double scale for damage.
Use live positive/zero checks without decimal arithmetic on targeting paths.
Keep native HP rounding, defenses, ownership and guest replication unchanged.
Prefer fixed precision over unrestricted floating input to keep preset equality
and display exact. Add precise text input alongside the existing slider.

**Stack/constraints:** C# 8/netstandard2.1, Unity/TMP, current shared session
services. Host authority; no custom guest state. All builds/tests use
`-p:DeployMod=false`. Do not deploy or launch the game. Commit/push after checks.

- [x] Add failing command/preset cases in `tests/SephiriaOne.Tests/Features/FriendlyFirePolicyTests.cs`:
  parse `damage 0.1`, `0.9%`, `0.01`, `1.25`; preserve exact values and old whole
  presets; reject negative/out-of-range/nonfinite/exponent/overprecision input.
- [x] Update `Features/Combat/FriendlyFireSettings.cs`: hundredth-percent offset,
  `decimal DamagePercent`, `double DamageScale`, `bool HasDamage`, invariant
  `Number`; `decimal? Percent` command and bounded decimal parser with optional `%`.
- [x] Update `Session/Presets/SessionPreset.cs`: emit v19 only for fractional
  combat values, accept v1–v18 unchanged and v19 including duration/stat rows;
  canonical `0.##` serialization and atomic rejection remain. Update status text.
- [x] Test then update `FriendlyFireRuntime`, `FriendlyFireEffects`, and
  `FriendlyFireArtifacts`: cache the scale in the hit context; use `HasDamage`
  for existing admission checks. Exercise 1000 damage at 0.1%=1 and 0.9%=9,
  shields, reflected/debuff/artifact damage, off/zero and unchanged enemy hits.
- [x] Update `UI/SettingsPanelCombat.cs`: fractional slider rounded to hundredths,
  numeric input using the shared parser, Apply through the existing command,
  no writes during drag/typing; preserve draft/scope invalidation. Update EN/KO.
- [x] Add command/reconnect/save/deathmatch regressions to runtime tests and
  verify UI wiring against compiled IL. Run Debug/Release, combat/runtime/panel
  and portable/installed-game contracts, plus unchanged-tick performance probe.
- [x] Review and update feature/index/history/version. Document
  native minimum-hit/rounding and unverified live UI/multiplayer behavior.

Verification: initial parser test failed on `0.01`; final suites pass (694 combat,
1,452 runtime, 175 combat policy/preset, 69 Bat, 22 panel, full native contracts).
Debug/Release builds have zero warnings/errors. Independent review has no
actionable findings. Deliver through the authorized conventional commit/push;
no deployment or live-game claim.
