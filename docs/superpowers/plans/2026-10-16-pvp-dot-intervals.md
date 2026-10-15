# Independent PvP DoT intervals implementation plan

**Goal:** Burn, Plasma Burn, Frostbite and Poison each use an independently configurable 0.75-second default periodic interval while PvP balancing is active.

**Architecture:** Reuse native periodic timers and current shared hostility/settings. Wrap the exact `Timer.Update` call with an adapter receiving its timer, native delta, debuff and explicit effect kind. Advance by `Time.deltaTime * timer.time / configuredSeconds` for eligible PvP, retaining native timer storage/progress and lifetime; normal native speed input resumes immediately outside the policy. Wound retains its existing 0.75 elapsed multiplier. Shock, Freeze, Dark Cloud and Plasma's shock accumulator remain separate and unchanged. No extra per-frame pass, actor cache or RPC.

**Scope:** Configured seconds are actual intervals before frame granularity, independent of BURNSPEED during balanced PvP. Range 0.05..10 seconds, two decimals; zero/invalid values rejected. Four independent default-safe integer settings, shared `/one friendlyfire dot burn|plasma-burn|frostbite|poison N|reset`, status, UI and presets. Edits never stop matches/reset KDA/budgets. Native PvE and system poison stay unchanged. Host-only installation; stock guests receive native damage/FX.

## Tasks

- [x] Write failing command/default and native timer fixture tests. Run `dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false` and the FriendlyFireTests project; confirm missing behavior.
- [x] Add focused `Features/Combat/PvpDotSettings.cs` value model and integrate with FriendlyFireSettings/Command, SessionCombat, preset v28 and test project links. Test range/precision, independent edits/resets, all copy paths, old presets, canonical rows and atomic rejection.
- [x] Replace only audited timer calls in FriendlyFireDebuffBalance with the adapter. Extend Burn/Elemental cadence fixtures for exact per-effect intervals, native timer lengths, BURNSPEED, live edits, paused time, PvE, system/self/companion, reconnect, shared scope and no allocations. Extend installed-game IL contracts, including Timer.time and native-only charge/duration paths.
- [x] Add compact DoT subsection beside Combat/PvP balancing, four independent fields in a 2x2 layout within existing bounds, shared command Apply and per-field drafts. Retain participant/master/KDA controls. Add EN/KO catalog keys and UI contract checks.
- [x] Extend runtime tests for live edits during all modes, restart/rejoin, save/load and full reset. Nondefault intervals use v28; older presets inherit 0.75.
- [x] Run non-deploying Release build and all affected suites, request independent code review, fix findings, update docs/version and commit/push per AGENTS.md. No deployment or game launch.

## Native findings

Installed assembly SHA-256 `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`. `Timer.time` is public; `Timer.Update` resets elapsed to zero on tick and preserves activeOnce/resetOnTime behavior. Burn and Plasma use BURNSPEED on a default 0.5s timer; Frostbite and Poison default to 1s. Plasma's separate addDamageTimer and base CharacterDebuff expiry must remain native. Prior addon behavior was a 0.75 delta multiplier, not a 0.25s DoT interval; 0.25s applies to separate Shock/Freeze/Dark Cloud limits.

## Results

Release build: zero warnings/errors. Combat 3,877; runtime 2,254 plus 69 Bat; interval policy/preset 343; catalogs 3,557. Installed-game/stock replication/UI contracts and 32 co-installed Harmony references passed. All four warmed timer paths add zero allocations. Independent review found no actionable issues. Live gameplay/UI remains unverified. No deployment or game launch. Integration follows the standing commit/push authorization.
