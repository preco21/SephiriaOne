# Addon reload and lobby combat investigation

2026-10-18, `0.65.1`. Investigates lobby-wide loss of friendly-fire damage,
unavailable match/revive actions, possible nine-player limits, and skipped hits.

## Confirmed reload failure

The local `Player.log` records three `Loaded v0.65.0` messages in one process
without an intervening addon unload. Later loads report friendly-fire
`Native before-attack callback boundary changed`, lobby-recovery
`Recovery call site changed during patching`, revive-tail, Mystic Jar and event
spawn transpiler failures. Database-ready callbacks occur once after the first
load, twice after the second and three times after the third. Private log contents
are not copied into the repository.

Installed native code explains this sequence:

- `GameDataLoader.Awake` calls `AddOnLoader.LoadAll`.
- `LoadAll` clears `_loadedMods` without unloading those entries.
- Each `TryLoadMod` uses `Assembly.LoadFrom` and constructs another entry object.
- `GameDataLoader.OnDestroy` clears its own singleton and unregisters its
  internal mod, but does not call `UnloadAll` for addons.
- A later `UnloadAll` sees only the most recently constructed entry wrapper.

Previously every `Entry.OnModLoaded` installed static hooks, subscribed another
database callback and created another persistent controller object. The field
guard for the controller was per entry instance, so it did not protect a new
wrapper. HarmonyX appends patches; it does not deduplicate identical methods.
Applying our own friendly-fire transpiler twice in an isolated fixture reproduces
the exact callback-boundary exception without any other addon patch.

Failed Harmony rollback is not reliably atomic: HarmonyX rebuilds the wrapper
while removing individual patches, so a duplicated failing transpiler can abort
cleanup before that transpiler is removed. A feature's earlier successful
availability flag can then coexist with damaged hooks. This explains why an
apparently enabled friendly-fire setting does not establish that damage hooks
remain installed. It also explains concurrent revival/recovery availability
failures. It is a strong cause for the reported symptoms, not a live multiplayer
reproduction of every observed missed attack.

## Lifecycle correction

Addon initialization now has one shared lifetime for the loaded assembly. A
repeated load updates the latest loader wrapper and reuses the original resource
owner; it does not reinstall hooks, resubscribe or recreate controllers. Unload
from the latest wrapper tears down the original owner's resources. Unload from
an older wrapper cannot tear down the current lifetime.

Successful repeated scans log `Reusing active runtime after addon reload.`
instead of a second addon initialization message.

Initialization and cleanup are explicit transitions. Reentrant transitions are
rejected. A failed initialization or cleanup retains ownership instead of
allowing a new load to stack more hooks on partially initialized state. Cleanup
can be retried; successful cleanup permits a fresh load. Existing recovery-first
shutdown ordering and native compatibility checks remain in place.

This is an assembly-lifetime fix, not an in-process repair of hooks already
corrupted by an older binary. Fully exit and restart the game after installing
the corrected build. Multiple physical addon copies or distinct assembly
versions loaded concurrently are outside the supported installation layout;
retain only one installed copy.

## Nine-player and lobby paths

Participant receipts, rosters, score records and damage budgets are dynamic
collections. No eight-player cap was found in the shared combat paths. A receipt
uses the exact current avatar, spawner, identity and connection, plus session
and lifetime generations. Reconnection never reuses the old explicit selection.

New nine-player runtime fixtures cover lobby readiness, casual participation,
an initializing ninth player, reconnect/selection reset, all four mode countdowns,
pair/team eligibility, explicit first/ninth selection, balancing-off edits, shared
stop and native recovery for the ninth player. An initializing player may block
a new command's all-ready preflight, but does not disable combat among already
ready participants. Tournament, Play Tag and Zombie roster restrictions remain
intentional; they do not admit a replacement connection as the old participant.

Native `UnitAvatar.ApplyDamage` still rejects peace mode, invulnerability, dead
targets and native immunity conditions. `CombatManager.PeaceMode` reads its
peace counter/session flag, not party size. The fix does not bypass native
cutscene/settlement protection. Lobby recovery blocking remains a separate
setting; automatic healing can otherwise obscure successful damage.

## Damage that can legitimately appear skipped

The balanced damage allowance is per directed attacker/opponent lifetime pair.
At the current defaults it holds 20% of victim maximum HP and refills at 70% of
maximum HP per second. Multiple hits in one frame can exhaust it; later hits
resume as time replenishes the allowance. With a 100-HP victim and resolved
20-damage hits, a controlled fixture yields `12.5, 7.5, 0` in one frame, then
approximately `7` after 0.1 seconds. Turning balancing off immediately permits
the normal 20-damage hit even if the previous allowance is empty. Nine-player
fixtures verify both one attacker against eight victims and eight attackers
against one healed victim use independent allowances.

Other deliberate filters remain: Dark Cloud admits one strike per 0.5 seconds
per pair; Shock/Freeze damage pulses each use 0.25 seconds; configurable Burn,
Plasma Burn, Frostbite and Poison intervals default to 0.75 seconds. Optional
hit-frequency scaling softens rapid hits before the per-second allowance.

One separate native interaction remains: MP shields call `UseMp((int)damage)`
then consume the entire hit. A sub-one damage value therefore costs neither MP
nor HP. With resolved damage 1 at 8 hits/sec for 80 hits, hit-frequency scaling
off costs 80 MP; enabling it costs 4 MP and produces 76 zero-loss hits in the
fixture. Budget refunds work, but do not aggregate fractional shield damage.
This task preserves that existing rounding contract; it is not a general lobby
failure or evidence of lost network messages. Unshielded HP is a float, although
the native displayed damage number is truncated to an integer.

## Evidence and verification limits

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA identity evidence:
`ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`.
Native types were inspected with ILSpy; decompiled sources remain outside Git.
The observed process loaded RaidRaid's HarmonyX 2.9 before this addon; duplicate
own-hook reproduction establishes that another mod's gameplay patch is not
needed to produce this failure.

Relevant executable suites: `SephiriaOne.EntryLifecycleTests`,
`SephiriaOne.RuntimeTests` (`--participants` for focused large-party coverage),
`SephiriaOne.FriendlyFireTests` and installed-game contracts in
`SephiriaOne.Tests`. Use `-p:DeployMod=false` for every invocation.
Verified locally: 38 entry-lifecycle checks, 2,755 runtime integration checks
(plus 69 Bat lifecycle checks), 4,925 friendly-fire executable hook checks,
Release build and installed-game contracts including all 33 compiled Harmony API
references against the installed HarmonyX 2.9 library. The focused participant
suite passes 80 checks, including the new nine-player cases.
Fixtures and static contracts do not prove live Unity/stock-client delivery.
No game launch or deployment is part of this investigation. The next live check
is a full restart with the new binary, a nine-player lobby fight, returning to
title and rejoining, followed by each combat mode with balancing on and off.
