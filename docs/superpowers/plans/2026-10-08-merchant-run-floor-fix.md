# Merchant run and floor correction

**Goal:** Keep minimum-floor conditions and HP scaling aligned with main dungeon
stages, and prevent a restart from seeding the new run with old dungeon progress.

**Evidence:** Host logs show added merchants during native stage 1 after multiple
maps. `MerchantRoute.FloorNumber` currently counts those maps. Native restart
replaces `CurrentRun` before reloading the reused dungeon manager. Native merchant
initialization does not apply HP bonuses a second time.

**Design:** Preserve route positions, saved targets, consumed rolls and caps.
Give every route position its main-stage number for eligibility and HP. Filter
guarantee candidates using that number. Safe lobby has no numbered opportunity;
a playable lobby uses factor 1. Optional maps inherit current main-route stage;
unknown progress uses factor 1. Native stage/co-op bonuses remain unchanged.

- [x] Reproduce fresh save/old dungeon overlap in MerchantTests, then guard native
  Seed/CurrentGame identity before creating or writing run-scoped state.
- [x] Reproduce multiple maps within stage 1 in MerchantRouteTests. Link real
  scheduling logic so `from 2` excludes every stage-1 candidate, including cap
  reservations and targets selected on a fresh run.
- [x] Store stage numbers separately from map positions in MerchantRoute; use
  them in MerchantSchedule filtering and MerchantRuntime spawn context.
- [x] Check mid-run minimum changes, toggles, copied saves, repeated fresh runs,
  unknown/board routes, health factors and retained native percentage bonuses.
- [x] Add per-spawn diagnostics, update EN/KO help and current docs, bump 0.28.2.
- [x] Run Release console suites and installed-game contracts; build Debug and
  Release with `-p:DeployMod=false`; independently review and fix findings.

Delivery uses the authorized Conventional Commit and push workflow on main.
Never run deployment. Independent review found no remaining actionable issues;
all 26 installed race assets support the stage ordering used here.

Validation commands run from `C:\Users\preco\repos\SephiriaOne`:

```powershell
dotnet run --project tests/SephiriaOne.MerchantTests -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.MerchantRouteTests -c Release -p:DeployMod=false
dotnet build SephiriaOne/SephiriaOne.csproj -c Debug -p:DeployMod=false
dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false
```

Live Unity verification remains separate: repeat a run with `from 2`, change
conditions during the first run, and compare stage-1/2 encounters and host/guest
health. Do not claim fixture tests reproduce a live multiplayer run.
