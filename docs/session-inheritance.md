# Settings for players joining later

## Behavior

Version 0.9.0 remembers successful host `/fountain`, `/choices`, and `/stats`
commands in memory for the hosted session. Newly ready avatars inherit the active
settings once. Since `0.11.0`, relative character-stat offsets are also maintained
when native inputs change, without stacking the bonus. Other settings remain
one-time adjustments. Names retain their existing ownership: newcomers see the addon
user's gradient through native name synchronization; their own names are unchanged.

- Character-stat add/subtract commands accumulate an offset applied to each
  player's own native stats, including current equipment and buffs. `luck +10`,
  then `luck -3`, gives seven additional luck. `luck set 100`, then `luck +10`,
  switches to native +10. Live commands, inheritance, saved presets, and automatic
  maintenance share the same planner. See [relative-stat consistency](relative-stat-consistency.md).
- Fountain `set` establishes an absolute target for newcomers; subsequent
  add/subtract commands adjust that target. Without a preceding set, Fountain
  deltas accumulate an offset applied to each newcomer's native starting points.
- Choice settings remember the extra-candidate contribution per category, leaving
  the newcomer's equipment bonuses intact.
- Reset removes both current tracked contributions and that selection's retained
  setting. `/stats reset` clears all retained character-stat settings; other
  families are independent. Setting Fountain/luck to zero remains a set operation.
- Failed commands do not change retained settings. Existing command validation,
  host authority, stat units, and exact multiplier handling remain in force.
- Retention uses commands issued in the current session or, since `0.10.0`, an
  explicitly [saved addon preset](session-preset.md). It does not reconstruct
  set-versus-relative intent from older versions or native game saves.

Settings survive floor changes and native in-host run restarts while the same
server and dungeon instance exist. Existing avatars do not receive another copy
on a run restart. Since `0.9.1`, the SDK session-start event also schedules a
[Fountain carryover limit repair](fountain-run-restart.md) after the game reloads
dungeon constants, without replaying point/stat/choice adjustments.
Active settings clear when the server stops, the dungeon instance changes,
or the addon unloads. `/mod save` writes a separate addon preset for future hosted
sessions; it does not edit the game profile. Without a saved preset, a new hosted
session starts without retained adjustments. A reconnect
with a new avatar inherits once against that avatar's restored native baseline;
any existing contribution markers are removed before applying the retained
setting, preventing duplicate adjustments if native state already contains them.

If any inherited setting is invalid for a newcomer (for example subtraction below
zero or an exact stat value that their multiplier cannot represent), that avatar
receives none of the inherited changes, and the host gets one warning. Existing
players are unaffected. Explicit commands can then configure/reset the group.
The failed inheritance is not retried continuously or applied silently after a
later equipment change.

## Readiness and synchronization findings

Since `0.12.0`, the shared coordinator tracks avatar object lifetimes, so a reused
network ID cannot skip inheritance. Command preparation, native writes, cap repair,
relative maintenance, and critical Fountain/candidate reads use shared validation
and outcomes. See the [implementation guide](synchronization-guide.md).

The installed `PlayerSpawner` receives default profile data before synchronously
initializing race, equipment, costume, inventory, and native stats. A nonzero
network ID alone is too early. The session controller polls in `LateUpdate` and
requires an active server, ready connection, avatar/race/name/floor, and a ready
server inventory with `canBroadcast > 0`. Native inventory restoration decreases
that counter until completion. The same readiness check guards manual commands.

There is no dedicated player-ready event in the inspected HorayMod API. This
inheritance feature requires no custom network message. Since `0.11.1`, a narrow
Harmony prefix also reconciles ready state before Fountain granting; see the
[lifecycle audit](sync-lifecycle-audit.md). Native
SyncDictionary/SyncVar writes deliver the inherited values to unmodified guests.
Before a host command, pending ready newcomers are processed first, so a command
entered in their arrival frame cannot skip earlier settings or double an offset.

## Implementation plan

1. Add portable tests and a compact `SessionPolicy` for command composition,
   reset removal, atomic newcomer planning, preserved native contributions,
   no-duplicate avatar tracking, readiness, and session boundaries.
2. Reuse the existing Fountain/choice/stat planners via internal constructors.
   Extract the Fountain write helper so manual and automatic updates share the
   same point/cap/reset-marker handling. Share candidate keys with the planner.
3. Add a persistent `SessionSettingsController`, ready-player collection, one-time
   application and host feedback. Hook successful commands into policy recording;
   reject unready avatars consistently and clear state on unload/server changes.
4. Update version and docs. Run tests, Debug and Release builds with
   `-p:DeployMod=false`, installed-game compatibility inspection and code review.
   Verify installed DLL/metadata hashes remain unchanged. Do not invoke the deploy
   script. Commit and push the verified source changes.

## Verification results

Verified on 2026-09-24 against the installed game recorded in
[development notes](development-notes.md):

- Debug and Release builds passed with zero warnings/errors using
  `-p:DeployMod=false`; the Release assembly version is `0.9.0.0`.
- 353 portable checks passed, including 43 session-policy checks. These cover
  relative/absolute composition, individual resets, existing contribution
  markers, amplified stats, negative/overflow results, and no partial plan.
- 38 additional integration checks run the production command and session
  services against minimal game API fixtures. They cover staged readiness,
  joining during a command, no repeated application, reconnects, rejected commands,
  reset retention, host authority, stopped servers, dungeon replacement, and unload.
  The fixtures do not run Unity, Mirror transport, or native initialization.
- Both existing candidate transpilers matched the installed game methods; the
  embedded Harmony runtime and license were verified. Code review found no
  actionable issues.
- No deployment script was invoked. The installed DLL and metadata hashes were
  unchanged from the task's start. Only `SephiriaOne.dll` is emitted as a Release
  DLL; no game assemblies are distributed.

Run both test suites (these commands do not deploy):

```powershell
dotnet run --project .\tests\SephiriaOne.Tests --configuration Release -- `
  'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed' `
  '.\SephiriaOne\bin\Release\netstandard2.1\SephiriaOne.dll'
dotnet run --project .\tests\SephiriaOne.RuntimeTests --configuration Release
```

## Live checks

After manually installing the new build and restarting, issue Fountain, candidate,
and luck commands as host, then invite an unmodified guest with different native
stats. Verify exact set targets, relative offsets, candidate bonuses, point caps,
and reset baselines on both clients. Join during a host command, reconnect, move
floors, reset before another join, and try an incompatible guest multiplier or
negative result. Confirm no repeated application, one warning on rejection, and
no retained settings after leaving the hosted session. Actual network delivery
and Unity lifecycle behavior still require these live checks.
