# Mystic Jar spawn rates

Added in `0.32.0`. The host can change the chance at normal random Mystic Jar
locations. Unmodified guests receive the result through the game's existing
object visibility synchronization. Default/reset leaves native chances intact.

Open `/one ui` and select **Jars**, or use:

| Command | Effect |
| --- | --- |
| `/one jars chance 50` | Set each eligible random location to 50%. |
| `/one jars chance x2` | Double each location's own native chance, capped at 100%. |
| `/one jars chance 0` | Disable normal random jar appearances. |
| `/one jars chance 100` | Fill all eligible normal random jar locations. |
| `/one jars reset` | Restore native chance; `off` and `chance x1` do the same. |
| `/one jars status` | Show active intent and compatibility status. |
| `/one save` | Save applied settings for future hosted sessions. |

Percentages allow two decimal places. Multipliers use the shared 0–10000 factor
range with two decimals. Repeating a multiplier replaces the setting; it does not
compound an already modified chance. The native class default is 18%, but maps
can supply different per-location chances, which remain the multiplier baseline.

## Scope

The user selected **normal random spawns only**. Hidden-room reward selection and
the jar reward's own native appearance behavior are excluded. Guaranteed
placements and minimum-chapter requirements also keep native behavior.

This setting changes existing random locations, not map layout: 100% does not
create new locations or guarantee a jar on every floor. It affects future server
generation checks. Already generated jars are not rerolled, refilled or removed.
Settings changed after a floor has generated take effect at later generation.

## Implementation and synchronization

`MysticPot.OnStartServer` uses its seeded `RandomID` roll, `useRandomAppear`,
`minChapterNum` and `appearRate` to write the native `isGenerated` SyncVar. A
validated Harmony transpiler replaces only the single `appearRate` read. The
seed, RNG call count, chapter checks, native field and SyncVar write stay intact.
An explicit zero override uses a negative comparison threshold because the
native test is `draw > chance`; this excludes the rare exact-zero draw as well.

`HiddenRoomRewardSpawner.OnStartServer` chooses among native rewards and calls
`SpawnProp` synchronously. An exception-safe, nesting-safe scope around this
method bypasses the override while the hidden reward spawns. Compatibility checks
validate both native paths; if they change, the feature stays unavailable and
reset remains possible.

The shared session service reads current policy at generation time, including
before its next update frame. Run restarts retain session intent; new hosted
sessions follow the existing explicit-preset loading rules. No per-player jar
state is cached. Rejoining guests receive the existing native object's state
instead of rerolling it. Host authority, command validation, snapshots, recovery
reset and v13 presets reuse the existing settings infrastructure.

There is no per-frame jar scan, extra RNG draw, prefab mutation, custom asset or
custom network message. Unload removes the hooks; future checks return to native
behavior without changing jars already generated.

## Verification and limits

Executable Release fixtures cover seeded native behavior, 0/100 boundaries,
per-location multipliers, chapter gates, guaranteed locations, hidden rewards,
nested/throwing hidden scopes, reset, nonserver calls, unload and changed native
instruction rejection. Runtime fixtures exercise shared commands, presets,
second runs, session changes, join/rejoin and compatibility failure. Portable
tests cover parser bounds, strict/atomic preset decoding and older schemas.
Installed-game IL checks verify the actual generation and visibility-sync paths.

These checks do not execute Unity's scene lifecycle or a live multiplayer
session. No deployment was performed. After manual installation, verify the
panel in EN/KO and compare 0%, 100%, x2 and reset on newly generated floors with
an unmodified guest. Confirm hidden rewards and chapter gates remain native,
existing jars stay unchanged, and guests rejoining the same floor see the same
objects. Save/restart and begin a second run to confirm retained policy.
