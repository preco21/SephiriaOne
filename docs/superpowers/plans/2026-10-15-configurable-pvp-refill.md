# Configurable PvP Refill Implementation Plan

> **For agentic workers:** Use the executing-plans workflow to implement this approved change inline. Refer to `docs/pvp-balancing.md` and current source for domain contracts.

**Goal:** Expose PvP damage-budget refill with a default of 100% of victim maximum HP per second.

**Architecture:** Extend the existing friendly-fire settings, command, snapshot and preset flow. Pass the configured fraction into the existing per-attacker/victim budget; preserve its tokens and debuff clocks during rate edits. No new polling, RPCs or player-stat mutations.

**Tech Stack:** C#, Unity uGUI/TMP, Harmony, existing executable .NET fixtures.

## Constraints

- Default refill 100%/s; editable 1..300%/s with up to two decimals.
- Preserve 20% burst capacity, per-hit softening, 0.25s Shock/Freeze clocks, and existing PvE behavior.
- `/one friendlyfire refill N|reset`; ordinary friendly-fire reset also restores 100.
- Retain settings across master-toggle/match overlays and run transitions; persist nondefault refill using preset v24. Older presets receive the new default.
- Rate edits must not stop matches, reset KDA, or grant a new burst. Conservatively use the lower of previous/current rates for the interval spanning an edit, then use the new rate.
- Keep the Combat panel's existing dimensions; add a separate refill input and Apply action. Translate English/Korean help, status and controls.
- Build with `-p:DeployMod=false`. Do not deploy or launch the game. Commit and push after verification, as authorized by AGENTS.md.

## Task 1: Shared configurable refill

**Files:** `Features/Combat/FriendlyFireSettings.cs`, `FriendlyFireDamageBudget.cs`, `FriendlyFireBalanceBudget.cs`, `FriendlyFireRuntime.cs`; `Session/SessionCombat.cs`, `Session/Presets/SessionPreset.cs`; `UI/SettingsPanelCombat.cs`, `SettingsPanel.cs`; localization catalogs; related test projects and domain documentation.

**Interfaces:** Settings expose exact decimal `RefillPercent`, double `RefillScale`, canonical `RefillNumber`; command accepts nullable `refillPercent`. `FriendlyFireDamageBudget.Take(damage, maxHp, now, refillPerSecond = 1d)` remains allocation-free.

- [x] Write regression tests using the current public parser and damage path, then observe failures: `/one friendlyfire refill 100` must parse; continuous damage must permit 20% + 100% HP in one second.
- [x] Implement validated settings/parser, canonical v24 persistence and status. Test defaults, fractional bounds, old-schema migration, malformed/duplicate rows, resets and toggle preservation.
- [x] Thread the rate into resolved PvP damage; test configured rates, subsecond kills, mid-fight edits without burst resets, max-HP changes, native shields/refunds and unchanged PvE.
- [x] Add the compact UI input and Apply control, independent draft state, and English/Korean translations. Verify installed-game UI contracts and match/runtime command behavior.
- [x] Update `docs/pvp-balancing.md`, `docs/domain-findings.md`, `docs/README.md`, version metadata. Run Release build, friendly-fire, runtime and installed-game contract suites with deployment disabled.
- [x] Review the complete diff; commit `feat(combat): make PvP refill configurable with a 100 percent default` and push main.

## Verification commands

```powershell
dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.FriendlyFireTests -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false -- 'C:/Program Files (x86)/Steam/steamapps/common/Sephiria/Sephiria_Data/Managed' 'C:/Users/preco/repos/SephiriaOne/SephiriaOne/bin/Release/netstandard2.1/SephiriaOne.dll'
```

**Review focus:** Same-frame configuration changes, nested damage refunds, zero-initialized defaults, deathmatch/tournament overlays, preset atomicity, UI draft lifecycle, no accidental deployment. Fixtures do not prove live multiplayer pacing or visual layout.

## Completion evidence

Release build: zero warnings/errors. Combat: 1,427 checks; runtime: 1,998 plus 69 Bat; dedicated refill: 129; catalogs: 3,375. Installed-game contracts passed. Review's sibling-draft finding was reproduced and fixed; per-field Apply cleanup preserves the other edit and scope transitions clear both. No deployment or live gameplay validation.
