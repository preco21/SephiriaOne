# Fountain carryover after returning to the lobby

## Report and root cause

On 2026-09-24, the user reported that an increased Fountain allowance worked on
the first run, but items beyond the normal allowance were missing on the next
run after returning to the lobby. Re-entering `/fountain` made it work again.
The installed addon and `Player.log` still showed `0.8.0`; the same gap remained
in the undeployed `0.9.0` implementation.

The log contains `Not enough Dimension Pocket capacity` for all four players
when the later run starts. Inspection of the installed game shows this path:

1. `HorayNetworkManager.RestartGameCoroutine` removes run items/statuses, then
   calls `NewGame` using the existing dungeon and player objects.
2. `DungeonManager.LoadDungeon` clears the synchronized constant dictionary and
   repopulates it from the game's defaults. This loses both the mod-raised
   `DIMENSIONPOCKETLIMIT` and its reset-tracking markers.
3. `NewGame` invokes the official `OnStartSessionServerside` event, then the
   restart coroutine synchronously calls each spawner's `RestartNewGame`.
4. The addon adjustment in `Inventory.dimensionPocket` survives independently
   of the reset dungeon limit. On entry to the run, the native item-grant method
   uses the smaller of those two values and stops at the first unaffordable item.

The old command raised the limit only when executed. The late-join controller
also deliberately skips already processed avatars, so a new lobby did not repair
that shared limit. Repeating a command raised it again, matching the report.

## Fix in 0.9.1

Subscribe to the official server session-start event and schedule a limit repair.
Process it in `LateUpdate` after native avatar initialization, using the existing
readiness checks. Ready players can restore their required allowance while an
initializing peer is still pending; finish once all available server players are
ready. Use current points from players covered by an active Fountain setting or
an existing addon contribution marker.

Reuse the Fountain planner with a zero delta, applying only the planned dungeon
limit and its reset markers. Do not replay point changes, character stats, or
candidate bonuses. Native point changes during reinitialization remain intact.
An explicit set equal to a player's native points still counts as an active
setting even when its contribution marker is zero.

Record the new lobby's native limit when raising it. `/fountain reset` then
restores that limit and each player's own baseline. Preserve an already higher
native/other-mod limit. After the pending repair finishes, stop enforcing the
cap so independent later changes remain possible. Clear pending work and detach
the event on unload; clear it on server/dungeon replacement too.

## Verification

- Added the SDK event boundary to the existing game API fixtures. The second-run
  regression failed against the old code before the production fix.
- All 71 command/session integration checks pass, including 33 new checks for
  repeated runs, separate player allowances, no stacked bonuses, deferred and
  mixed readiness, native point changes, resets, higher independent limits,
  absent Fountain settings, addon reload, unload, and host authority.
- All 353 portable checks pass. Installed-game candidate guard matching and the
  embedded Harmony runtime/license checks also pass.
- Debug and Release `0.9.1` builds pass with zero warnings/errors using
  `-p:DeployMod=false`. No deployment script was invoked; installed addon files
  remain unchanged.

These tests exercise production mod services with data fixtures. They do not
execute Unity, Mirror transport, or actual item granting. After manually installing
the Release DLL and metadata and restarting the game, repeat the original sequence
with an unmodified guest: set an allowance above 12, select items using that budget,
enter and finish a run, return to the lobby, then start another run without another
command. Check that all selected items carry over for each player. Test a third
run and `/fountain reset` too. The host log should include:

```text
[SephiriaOne] Restored Fountain carryover limit after lobby restart: 12 -> <allowance>.
```
