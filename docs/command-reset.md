# Reset commands

## Behavior

Version `0.6.0` adds explicit host-only reset commands for all current players:

- `/choices reset` or `/choices all reset`: remove all three addon bonuses.
- `/choices item reset`, `/choices weapon reset`, `/choices miracle reset`:
  remove only the selected category's addon bonus.
- `/fountain reset`: remove the net points adjustment made by this addon and
  restore the session carryover limit that it increased.

Version `0.7.0` also adds `/stats luck reset` (or another supported stat) and
`/stats reset` / `/stats all reset`. These remove only the tracked base-stat
contributions from character-stat commands, preserving native equipment/buffs.
`/stats luck 0` sets luck to zero; it is not a reset. See [stat commands](stat-command.md).

Since `0.9.0`, resets also clear the selected retained session settings, so future
joiners no longer receive those adjustments. Other categories and command
families remain active; see [session inheritance](session-inheritance.md).

Reset means the state without this addon's commands, preserving native upgrades,
equipment, and later independent stat changes. It does not mean zero Fountain
points or a fixed candidate count. Repeated reset is harmless. `/choices all 0`
and per-category zero remain equivalent to resetting their addon contributions.
Choice reset works even if the candidate generation guards could not initialize.

Fountain commands previously did not track their changes. Starting with `0.6.0`,
store the net adjustment in a namespaced player custom-stat marker. Track the
original and last-written session limit in namespaced dungeon constant entries.
Reset only lowers the limit if it still matches the last value written by this
addon; preserve a subsequent independent limit change. Dungeon initialization
clears the limit markers with the dungeon constant dictionary. Point-contribution
markers belong to the avatar's custom-stat dictionary: they remain valid while
that avatar and its inventory retain the point adjustment, including native
additive stat changes. A new avatar starts without that marker or adjustment.
An untracked player is unchanged by reset. This cannot reconstruct commands
issued by older versions; fully restart the game after updating the binary.

Reset does not change already generated choices, saved Fountain item selections,
or granted items. Reopen the Fountain panel; use new offers or normal rerolls for
candidate changes. The same server synchronization reaches unmodified guests.

## Implementation and verification plan

1. Extend both parsers with strict, case-insensitive reset forms and reject
   extra reset arguments. Add tests before implementing parsing behavior.
2. Extend Fountain planning to take per-player contribution amounts, validate
   every result and accumulated offset before returning any updates, and handle
   tracked/untracked limits. Keep state in native synchronized dictionaries.
3. Reuse choice contribution removal for explicit reset and set-to-zero; bypass
   expansion-only bounds and patch availability when merely removing a bonus.
4. Cover different starting points, set/add/sub sequences, later native stat
   changes, negative adjustments, repeated/no-op reset, overflow rejection,
   per-category parsing, and preservation of independently changed limits.
5. Build Debug and Release, run portable and installed-game compatibility checks,
   review, update docs, verify deployment, and commit/push using Conventional Commits.

## Live checks

Record each player's unmodified Fountain points, issue set/add/sub commands, then
reset and compare every player. Repeat with a native stat change between command
and reset. Confirm the original carryover cap returns and other limit changes
are preserved. Test candidate category reset and all-category reset, repeated
reset, normal chat, rejected guest requests, and invalid command arguments.

## Verification results

On 2026-09-24, Debug and Release builds passed with zero warnings and errors.
All 156 checks passed: 21 name synchronization, 44 Fountain commands, 20 Fountain
reset scenarios, 63 candidate commands, and 8 generation-guard checks. The
installed-game guard compatibility checks and embedded dependency/license check
also passed. Review found no actionable code issues.

Release `0.6.0` was deployed to `AddOns\SephiriaOne`; DLL and metadata SHA-256
hashes matched Release output, and the assembly version is `0.6.0.0`. Live host/
guest resets and session transitions still require the checks above.
