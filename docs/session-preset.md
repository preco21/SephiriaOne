# Active settings and saved preset

Added in `0.10.0`. Only the host needs the addon; preset commands and their
feedback remain local. Guests receive gameplay changes through native sync.

Version `0.31.0` adds v12 for [friendly fire](friendly-fire.md). Optional rows
`friendlyfire enabled 1` and `friendlyfire damage 25` store the toggle and whole
percentage (0–300). Missing rows mean off/100%. Turning it off retains the chosen
percentage; reset clears both. Older schemas remain supported with safe defaults.
Invalid, duplicate or out-of-range rows reject the entire preset.

Since `0.13.0`, the [host settings panel](control-panel.md) provides the same
save/forget/status operations through `/one ui` or the pause-menu button.
Its Presets tab separates active intent from the saved copy; saving excludes
unapplied input. It introduced no automatic-save rule.

In `0.15.0`, the commands move from `/mod` to `/one` with no old alias, to avoid
collisions with other addons. Presets containing [resources](resource-command.md)
use v3 and retain Set/Offset/Multiplier intent. v1/v2 files remain supported.
Run checkpoints preserving inventory/talent capacity and spent starting grants
are separate from the explicit future-session preset.

Version `0.20.0` adds v7 for the [extra hostile merchant](wandering-merchant.md):
`merchant spawns 1` enables it and `merchant chance 25` stores a custom whole-number
chance from 0 to 100. Missing rows mean off/25%. A custom chance persists even with
the toggle off. Earlier presets remain supported. Per-floor rolls and the randomly
scheduled encounter guarantee live in the native run save, not the future-session preset;
loading a preset or rejoining cannot reroll a consumed floor.

Version `0.24.0` adds v8 for [independent merchant variants](merchant-variants.md).
Rows such as `merchant papyrus spawns 1`, `merchant papyrus chance 10`,
`merchant papyrus from 3`, and `merchant papyrus limit 2` address one type.
Default values are omitted; custom conditions persist while disabled. Old v1-v7
presets still load and leave the new types off. Wandering-only toggle/chance presets
still export as v7 when possible. Duplicate alias/typed fields and unknown type IDs
are rejected atomically. Run counters, reservations and guarantees remain separate
from presets, so reloading settings never replenishes them.

Version `0.25.0` adds v9 for per-type guarantee toggles. For example,
`merchant papyrus guarantee 0` disables only Papyrus's guaranteed encounter while
leaving its spawn toggle, chance and conditions intact. Missing guarantee rows
default to on for all current types, including v1-v8 presets. Explicit `1`
normalizes away; exports retain the oldest applicable schema when possible.
Guarantee rows require v9, a canonical type ID and a canonical `0` or `1`;
duplicates or invalid values reject the entire preset. The setting persists even
while that type's spawns are off. Run targets, completed guarantees, consumed
floor rolls and counts survive option changes; turning a guarantee back on never
resets them or bypasses the current cap.

Version `0.28.0` adds v10 when [Rabbit level-up potion rewards](rabbit-level-up-potions.md)
are enabled: `rabbit level-up-potion 1`. All previous setting families, including
typed merchant conditions and guarantees, remain supported in v10. Missing rows
and v1-v9 presets leave rewards off. Loading a preset changes future level-up
behavior; it never grants items for past levels. Invalid/duplicate rows reject
the whole preset. Off/reset return exports to the oldest applicable version;
older addon versions cannot load v10.

Version `0.30.0` adds v11 for [given-item restriction unlocking](item-restrictions.md):
`items unlock 1`. Missing rows and v1-v10 presets keep the default off. Invalid,
duplicate and old-version item rows reject the whole preset. All other option
families continue to work in v11; disabling this option exports the oldest
applicable version. `/one save` persists intent, while native run saves always
retain the original restriction metadata so resuming without the addon remains
safe. New/rejoining players use the current shared metadata, not a saved per-player
permission.

Version `0.16.0` stores enabled [rabbit potion options](rabbit-potions.md) in v4
as `rabbit infinite 1` and `rabbit share 1`. Missing/off options remain native.
v1-v3 are still readable; v4 supports all prior setting families. These flags are
read at potion use, never translated into healing on preset load or player join.

Version `0.17.0` adds v5 rows `rabbit mp-cost 1` and
`rabbit suppress-survival 1`. The four options remain independent and reset
together with `/one rabbit reset`. v1-v4 still load and leave new flags off;
loading or rejoining never charges MP or triggers/suppresses a past potion event.

Version `0.18.0` adds v6 for custom Rabbit MP fees: `rabbit mp-amount 25` stores
the integer amount independently of `rabbit mp-cost 0|1`, which retains its boolean
meaning. Both row orders work. Disabled custom fees are preserved. Missing amount
rows and v1-v5 files use 10 MP. Amounts must be canonical integers 0..10000; invalid
or duplicate rows reject the entire file. Only a nondefault fee requires v6.
Older addon versions cannot load v6. `/one rabbit reset` restores 10 MP and all
Rabbit toggles off; save again to persist that reset.

Since `0.14.0`, [native-baseline factors](multiplier-command.md) are saved as
`multiplier N` rather than calculated totals. A preset containing a factor uses
v2; v1 files remain supported and are still written when no factor is active.
Older addon versions cannot load v2. Status exposes the retained factor even
when native incompatibility temporarily suspends its application.

Native in-game loadout presets are separate from this addon file. Applying one
does not replace the retained addon policy; its equipment/passive effects update
native baselines. See the [lifecycle audit](sync-lifecycle-audit.md) for these
transitions and cached UI limitations.

## Usage

```text
/fountain set 100
/choices all 5
/stats luck +10
/one status
/one save
```

After exiting and launching the game again, host a session. Each ready player
gets 100 Fountain points, five extra candidates in each category, and ten luck
above their own native value. Since `0.11.0`, the host maintains relative stat
offsets after gear/buff changes. `/one status` shows the active and saved copies
separately, plus current player values. Player labels use native network IDs.
Stat totals use the displayed units from `/stats list`; any `base adjustment`
shown in parentheses is the tracked raw base-stat contribution used for reset.
If changed multipliers cannot represent a relative offset, status labels it as
suspended. Saving still records the requested offset, which may be rejected for
an incompatible player when inherited in a future session.

To change the saved copy, issue new commands and run `/one save` again. To stop
future automatic loading, run `/one forget`. To also undo the current adjustments,
use `/fountain reset`, `/choices reset`, `/stats reset`, `/resources reset`, and `/one rabbit reset`. These resets alone
do not erase the saved preset.

## Design

Since `0.12.0`, status also includes shared synchronization outcomes, revisions,
critical-read availability, native name synchronization status, and partial-write
journals. Saving is blocked
while native writes are faulted. See [diagnostics and recovery](synchronization-guide.md#diagnostics-and-recovery).

Host-only local chat commands cover all gameplay families:

- `/one status`: show retained session adjustments, current adjusted values for
  each ready player, and the saved preset for future hosted sessions.
- `/one save`: explicitly replace one saved preset with the current retained
  Fountain, choice, character-stat, resource, and rabbit potion settings.
- `/one forget`: remove the saved preset without changing the current session.
- `/one` or `/one help`: show usage.
- `/one ui`: open the host settings panel while in town or a run.

Preserve command intent rather than capturing equipment: a retained set remains
absolute, a net add/subtract remains relative to each player's native baseline,
and choices retain their extra contribution. Status labels distinguish these
retained settings from effective current values and base-stat contribution
markers. A set equal to a native value is still an active setting.
For character stats, an add/subtract command after `set` switches to a new relative
offset; subsequent deltas accumulate. Older presets retain their explicit stored
mode and amount: `set 110` stays absolute, while `offset 10` is now maintained
automatically. Those modes remain compatible with v1. See the
[relative-stat audit](relative-stat-consistency.md).

One explicit snapshot is sufficient; named presets and continuous autosave are
outside this change. Resets and subsequent commands affect only the current
session until another `/one save`. Saving an empty session is valid; `/one forget`
removes the file. Existing unsupported/legacy adjustments cannot be reconstructed
as set-versus-relative intent and must be reissued before saving.

Store a versioned, bounded UTF-8 text data file at
`Application.persistentDataPath/SephiriaOne/session-preset.txt`. Validate all fields,
known categories/stats, duplicates, amount precision and ranges before accepting
the whole file. Never interpret it as a script. Write through a unique temporary
file and atomically replace the previous preset; failed saves leave it intact.

Read once when a new hosted server/dungeon becomes ready. Install the saved policy
before processing players through existing once-per-avatar inheritance. This
applies to the host, guests, and later arrivals using native synchronization.
Existing markers prevent stacking when restoring an avatar that already contains
an adjustment. Run restarts keep the active policy and Fountain cap repair without
reloading or replaying the saved preset. Guests cannot save or apply host settings.
Invalid files warn once per session and leave native gameplay active. Existing
per-player planning rejects incompatible multipliers/underflow atomically.

## Implementation plan

- [x] Add pure policy export/import, bounded versioned codec, and `/one` parser.
  Test composed set/relative history, all categories, decimals, zero targets,
  resets, malformed/duplicate/unknown fields, bounds, and invariant culture.
- [x] Add isolated file storage with atomic replacement and explicit deletion.
  Test save/load/overwrite/forget, empty/missing files, invalid data, and failure
  preserving the previous file using temporary directories.
- [x] Integrate host-only commands and read-only status with chat. Load saved
  policy at hosted-session initialization before avatar processing. Extend runtime
  fixtures to isolate persistent paths and verify reload, late join, reconnect,
  same-session restarts, resets versus saved state, host authority, and bad files.
- [x] Build Debug/Release with `-p:DeployMod=false`; run both suites and installed
  game compatibility checks. Review code, update docs/version, verify installed
  hashes unchanged, then Conventional Commit and push `main`.

No deployment script will run for this task. Test services with temporary data;
do not create a real saved preset for the user's game during development.

## Verification

On 2026-09-24, all 400 portable checks (including 47 preset/parser/storage checks)
and 98 command/session integration checks passed. Debug and Release builds passed
with zero warnings/errors, deployment disabled. Installed-game candidate guard
matching and embedded dependency/license checks passed. Test files used isolated
temporary directories; no real user preset was created or changed.
Code review found no actionable issues. The installed addon DLL and metadata
hashes remained unchanged; version `0.10.0` was not deployed.

The runtime fixtures cover a fresh hosted session, late joiners, existing markers,
same-host lobby restarts, status remaining read-only, saved versus current resets,
forgetting, malformed files warning once, incompatible multipliers, and host-only
access. Persistence checks include a locked destination and keeping the old file
when replacement fails.

Live Unity checks remain pending: manually install the new DLL/metadata, restart,
run the example above, and verify status lines in the game log. Exit fully and
host again with an unmodified guest; verify values before and after returning to
the lobby. Test `/one forget` and an explicit reset followed by `/one save`.
