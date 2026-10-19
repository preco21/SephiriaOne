# Starting Zombie count

User-authorized implementation, 2026-10-19.

Goal: expose a host-only positive whole-number starting count, default/reset 1,
through `/one zombie count N|reset` and the Zombie tab. Preserve the requested
count in settings; clamp the match snapshot to 1..eligible participants minus 1.
Fewer than two eligible players still cannot start. Edits after Start affect the
next match, including edits during countdown. Never reroll an active team.

Architecture: add the count to SessionZombiePolicy/SessionZombie and preset v32.
Older presets retain count 1 and reject the new row. Capture alongside the
frozen Zombie roster. Uniform sampling without replacement belongs to the pure
ZombieSurvivalRound model; initial Zombies are combat-active, not pending
infection. Reuse existing role visuals, announcements, hostility and cleanup.
Add one compact input row with normal Apply/Reset, draft cleanup and EN/KO text.

- [x] Add failing command/preset and runtime regressions: counts 1, 2 and excess;
  distinct roles; at least one Human; selection; nine players; countdown edits;
  next-match adoption; authority and malformed input; saved settings and reset.
- [x] Implement settings, strict v32 persistence, snapshot and random team seeding.
- [x] Add UI/EN/KO messages, update feature docs and increment addon to 0.66.0.
- [x] Run Release runtime, friendly-fire and portable/native contract suites with
  `-p:DeployMod=false`; inspect final diff. Commit/push follows repository workflow.

Verification: `dotnet run --project tests/SephiriaOne.RuntimeTests -c Release
-p:DeployMod=false`; build the addon and run SephiriaOne.Tests against installed
managed assemblies plus the Release addon and installed HarmonyX. Existing
FriendlyFireTests cover model callers and team-based targeting. No new native
hooks, assets, per-frame allocations or guest addon requirement.

Results: first runtime regression failed on the missing count command, then
passed after implementation. Final Release build: zero warnings/errors; 3,134
runtime plus 69 Bat checks; 4,944 friendly-fire checks; portable/localization and
installed native contracts passed, including compiled UI wiring and HarmonyX
API compatibility. Updated unknown-preset sentinel to v33 now that v32 is valid.
Independent read-only review found no correctness issues; its suggested
activation-callback/readiness-retry regression was added and passed. No live UI
or multiplayer session was exercised. Nothing deployed.
