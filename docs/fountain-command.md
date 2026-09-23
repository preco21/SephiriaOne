# Wishing Fountain chat command

## Design

Add host-only chat commands that set or adjust Wishing Fountain points for every
currently spawned player. Solo play also runs a local server. Unmodified guests
receive the resulting points through the game's existing synchronization.

Since `0.9.0`, newly ready players also inherit the active Fountain setting once:
set commands retain an absolute target, while add/subtract without a prior set
retain an offset against each player's own starting points. Reset clears the
retained setting. See [session inheritance](session-inheritance.md).

| Command | Effect |
| --- | --- |
| `/fountain 100` or `/fountain set 100` | Set each player's points to 100. |
| `/fountain +10` or `/fountain add 10` | Add 10 to each player's current points. |
| `/fountain -5` or `/fountain sub 5` | Subtract 5 from each player's current points. |
| `/fountain reset` | Remove this addon's point adjustments and restore the session limit it raised. |
| `/fountain` | Show usage. |

Names are case-insensitive. Amounts are whole numbers. Validate every resulting
balance in the range `0..2147483647` before changing any player. Underflow,
overflow, invalid arguments, a non-host caller, or players still initializing
must leave everyone unchanged. Ordinary chat remains ordinary chat; only the
exact `/fountain` command token is consumed locally.
Only the host needs this addon. Command feedback stays in the issuing player's
local game log; guests should reopen their Fountain panel to see the new capacity.

## Installed-game findings

- The Wishing Fountain is `StartingFountain`, not the random-reward
  `MagicFountain`. `StartingFountain.OpenUI` supplies the player's
  `GridInventory.dimensionPocket` to `UI_DimensionPocketPanel`.
- `GridInventory.NetworkdimensionPocket` is the game's generated SyncVar setter.
  Use it on the server so changes are sent to clients.
- `PlayerSpawner.AddDimensionPocketItemsOnServer` caps the usable budget at
  `KeywordDatabase.GetConstValue("DIMENSIONPOCKETLIMIT")`. The installed resource
  `Const` defines the default as 12.
- `DungeonManager.constValueDictionary` is a synchronized dictionary. Raise its
  session `DIMENSIONPOCKETLIMIT` to at least the largest new balance. Never lower
  an existing higher limit as a side effect of decreasing player points.
- `UI_ChatInput.OnOpened` adds its private `OnSubmitCommand(string)` as an input
  submit listener; `OnClosed` removes it. That handler broadcasts
  `inputField.text`, not its callback argument. A mod listener installed before
  it can consume recognized commands by clearing the field. Normal messages
  continue through the game's existing handler.
- Fountain panels cache capacity when opened. Reopen them after changing points.
- Since `0.6.0`, namespaced synchronized markers record this addon's signed
  point adjustments and original/last-written session limit. Reset preserves
  native stat changes and independently replaced limits; see [reset behavior](command-reset.md).

## Implementation plan

1. Add a Unity-independent parser and batch planner with tests for command
   recognition, signed shorthand, aliases, invalid numbers, overflow, underflow,
   per-player deltas, and no partial mutation on validation failure.
2. Add a runtime service that checks host authority, gathers all live server
   player inventories, validates the complete batch, raises the synchronized
   carryover limit if necessary, and uses native SyncVar setters.
3. Add a chat listener owned by the addon. Use a reflected delegate only to order
   the already-existing game submit listener after the mod listener. Do not
   remove unrelated listeners or patch game methods. Unhook on scene changes and
   unload. If the expected method is absent, leave normal chat working and log
   the incompatibility.
4. Integrate the controller into addon load/unload; bump the version to `0.4.0`.
5. Run all portable tests, build Debug and Release, verify deployment hashes,
   review the change, then commit and push.

## Scope and lifecycle

This changes runtime point capacity; it does not buy or rewrite passive upgrades,
edit profile files, grant items directly, or keep enforcing a value every frame.
Normal game stat changes can subsequently adjust the capacity. New players and
replacement avatars inherit the active session setting once. Existing avatars
retain their point adjustment until reset or replaced. The raised session limit is
reset by the game's normal dungeon initialization. Unloading the addon removes
the command listener but does not undo an already applied points command.
Use `/fountain reset` to undo adjustments tracked from `0.6.0` onward. Players
with no tracked adjustment are unchanged, and repeated reset is harmless.
Normal upgrades are preserved; reset does not set everyone to zero or 12.

Items selected through the normal Fountain UI can be saved by the game as usual.
Reducing points does not delete previously selected items or items already
granted. Reopen the panel and review the selection before starting the run.

## Verification

Completed on 2026-09-23 against the installed game assembly recorded in
[development notes](development-notes.md):

- 40 Fountain parser/planner checks and 21 existing name-sync checks passed.
- Debug and Release builds passed with zero warnings and zero errors.
- Code review found no actionable issues in authority, chat listener ordering,
  all-player updates, validation, or cleanup.
- Release `0.4.0` was deployed to `AddOns\SephiriaOne`. Both DLL and metadata
  SHA-256 hashes matched the build output; assembly version is `0.4.0.0`.
  The only DLL in Release output is `SephiriaOne.dll`.

Run portable tests with:

```powershell
dotnet run --project .\tests\SephiriaOne.Tests --configuration Release
```

Live checks after restarting with the current build:

1. In solo town, send `/fountain 100`, then reopen the Fountain and check its
   allowance. Check `/fountain +10` and `/fountain -5` relative to that value.
2. Host with an unmodified guest and repeat. Verify both players' Fountain
   allowances, then select items costing more than 12 total and confirm the
   selection is honored when entering the run.
3. Confirm invalid commands and subtracting below zero leave all players
   unchanged. Check normal chat and `/fountainish` still send normally.
4. With the addon installed on a guest, confirm its command reports host-only
   access and changes nobody. Guest chat received by the host must not execute
   commands.
5. Reopen chat several times and check each command executes exactly once.
   Test title/session transitions and addon unload for listener cleanup.
6. Record each player's initial points, apply several commands, then use
   `/fountain reset`. Confirm each player regains their own normal value and the
   original carryover cap returns. Test repeated reset and a native stat change
   between the first command and reset.

Portable tests and builds cannot verify Unity event ordering, peer UI rendering,
or actual item carryover. Those checks require running the game.
