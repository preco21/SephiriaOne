# Random event room probability

Added in `0.35.0`. Hosts can multiply the native probability of optional event
rooms. Default/reset preserves native odds. Open `/one ui`, select **Spawns**,
then use the arrows to select **Random event rooms**.

| Command | Effect |
| --- | --- |
| `/one events chance x2` | Double the native cumulative event-room probabilities. |
| `/one events chance x0.5` | Halve those probabilities. |
| `/one events chance x0` | Disable optional random event rooms in future generation. |
| `/one events reset` | Restore native odds (`off` and `chance x1` also reset). |
| `/one events status` | Show the active multiplier and compatibility status. |
| `/one save` | Save applied settings for future hosted sessions. |

Multipliers accept 0–10000 with at most two decimal places. Repeating x2 stays
x2; it never multiplies an earlier override. Each probability is capped at 100%.

## Scope and timing

The native roll has a **4.3% chance of at least one optional room**, including
a **0.3% chance of two rooms**. Thus x2 means 8.6% for at least one, including
0.6% for two; the chance of exactly one is 8%. The native maximum remains two.
The floor's room pool and other native generation conditions still apply.

Inspected room assets include blood donation (`Wanderer_VampireBat`), obelisks,
magic fountains, witch hats and other optional encounters. This option adjusts
the probability of selecting optional rooms, not each room type's selection
weight or its own subsequent activation conditions.

Regular merchants and travelers (including Collin) are scheduled separately by
the native stage generator. Those schedules, the addon's hostile merchant
options and its Mystic Jar chances remain separate. The user's term “liver shop”
has not yet been identified; this feature does not claim specific coverage of it.
There are separate appearance gates on `ExpShop` and `InventoryShop`, which are
not patched by this feature.

**Changes apply when the game generates a chapter's floor data.** A chapter's
data is created together, potentially before visiting its first floor. Editing
the multiplier does not reroll existing or saved floor data, even if a floor has
not yet been visited. New chapter generation reads the latest setting. Session
intent survives a new run; a new hosted session loads only the explicitly saved
preset, otherwise native defaults.

## Implementation and multiplayer

`StageEntity_Choice.GenerateStage` and `StageEntity_GrasslandTown.GenerateStage`
each use a seeded draw against 0.003 and 0.043, then store `randomRoomCount` in
`FloorData`. A validated Harmony transpiler adds a threshold-adjustment call
after each constant. All original instructions, labels, RNG calls, generated
seeds, count assignments and other encounters remain intact. x0 uses a negative
threshold to exclude even the exact-zero result of the native `<=` comparison.

Shared host settings are resolved at generation time, including before the next
synchronization frame. The resulting count is saved in native floor data,
serialized by Mirror and passed into `FloorGenerator.Connect` and its native
`randomRoomCount` SyncVar. Both supported procedural room generators consume
that count and cap their selection to the available room pool.

Unmodified guests use these native results, including initial state after a
reconnect. There is no per-player event receipt, join-time reroll, extra network
message, new asset, or client addon requirement. There is no per-frame scanning
or additional RNG work. The hooks run only during chapter generation.

Command validation, host preflight, atomic policy changes, immutable snapshots,
status, reset and v16 presets reuse the shared settings system. Older presets
default event odds to native. Failed compatibility checks disable edits while
leaving reset available; unexpected policy lookup errors retain native thresholds
and log once. Unload removes both hooks without modifying already-generated data.

## Verification and remaining manual checks

Release Harmony fixtures exercise both generators over seeded rolls, multipliers,
zero/capping, unchanged subsequent RNG, repeated calls, client bypass, reset,
failure fallback, unload and changed-IL rejection. Shared runtime tests cover
commands, immediate generation reads, second runs, reconnects, saved reloads,
session replacement and host-only enforcement. Policy tests cover strict parsing,
atomic preset rejection, older schemas and independent Jar settings.

Installed-game IL checks verify both real generator contracts, preservation of
every original instruction, native FloorData and generator initial/incremental
serialization, and room-pool consumption. EN/KO catalog and existing Jar tests
are also checked. These checks do not execute a live Unity multiplayer session or
render the panel. After manual installation, compare x0, x2 and reset on newly
generated chapters with a stock guest, reconnect on the same floor, resume a saved
run, and start a second run. Confirm existing floor data does not change when
editing the setting and that Jar controls remain independent.

No deployment to AddOns was performed.
