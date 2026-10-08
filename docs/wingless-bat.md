# Wingless Bat HP steal

Added in `0.33.0`. Default **off**. The installed English name is **Wingless Bat**
(the requested “Wings lost bat”); Korean is **날개 잃은 박쥐**, costume ID `Bat`.

Open `/one ui` → **Costumes** (renamed from Bat in `0.34.0`), or use:

| Command | Effect |
| --- | --- |
| `/one bat hp-steal on` | Reduce Bat's own HP-steal bonus from 5 to 1. |
| `/one bat hp-steal off` | Restore the original costume bonus of 5. |
| `/one bat reset` | Same as off. |
| `/one bat status` | Inspect active intent and hook availability. |
| `/one save` | Persist applied settings for future hosted sessions. |

Only the host needs the addon. The option applies to every player wearing this
costume, including late joiners and returning connections. Other costumes and
HP steal from equipment, potions, buffs or other statuses remain unchanged.
Native stat amplification still applies: this sets the costume contribution,
not a hard cap of 1 on the player's total effective stat.

## Native ownership and synchronization

The installed `resources.assets` costume `11_Bat` contains `HP_STEAL/5` and
`FIXED_DASH/1`; the HP-steal status uses `StatusInstance_HPSteal`. Its native
Apply/Remove methods add/subtract its stored Value from `HPSTEAL` in the avatar's
Mirror SyncDictionary. Native healing and the character stat display consume
that effective stat, so no custom client RPC or guest definition is needed.

The factory call in `PlayerAvatar.UpdateCostumeData` is wrapped to change a
verified Bat HP-steal status's Value from 5 to 1 before native application.
Other status creation, costume items and costume effects retain native code.
Current settings are read at that event, without waiting for the next frame.

Live toggles use the shared host command preflight and write journal. Each write
holds the synchronized raw stat and owned status Value as one pair. The removal
amount changes before the native dictionary setter notifies callbacks, so even
a reentrant costume change subtracts the correct value. Pre-write rejection
restores the original removal amount; post-write failure retains the matching
new amount. Shared readback still reports failures and retains explicit recovery.

Native ClearTarget and avatar destruction release tracked ownership. Unequipping
subtracts the actual applied amount. New runs, saved-run reconstruction and
replacement avatars create fresh statuses; no per-network-ID adjustment survives
to contaminate a reconnect. Shared inheritance also covers already-equipped
players when a saved preset loads or the controller restarts.

Scope cleanup restores surviving owned statuses without replaying an expired
command journal. With authority lost it discards old tracking without attempting
network writes. Addon unload restores Bat before shared Choice cleanup and only
retires a Bat fault once its owned state has been restored. Unrelated feature or
inheritance failures retain the existing explicit-recovery requirements.

There is no per-frame polling or stat recomputation for this feature. Native
method/field contracts are validated before enabling hooks; changed/custom
status types or a native contribution other than 5 are left untouched.
Arithmetic overflow rejects a command before its writes begin.

## Presentation and persistence

The native costume-selection preview reads local asset definitions and continues
to show 5, on the host as well as stock guests. The actual character stat and
healing follow the host setting. The addon Bat panel and status command show the
active option in EN/KO; no local-only costume preview patch is added.

Preset v14 stores only `bat hp-steal 1`. Older presets leave it off. Native run
saves do not serialize the mutated status instance or raw custom-stat dictionary;
the game reconstructs costume contributions. `/one save` is still explicit.

## Verification

Debug and Release builds passed without warnings. The Release suite passed
69 Bat lifecycle checks (953 runtime checks overall), 24 Bat policy/preset checks,
1276 bundled-catalog checks and 48 localization-loader checks. The installed-game
compatibility suite passed. The existing five-player performance probe retained
zero steady synchronization allocations; it is a fixture, not live game profiling.
Independent review findings were fixed and rechecked before integration.

Release fixtures cover baseline 5→1→5, independent equipment/potion bonuses,
native amplification, repeated toggles, costume switches, repeated native
reconstruction, joins/rejoins with reused IDs, second runs, saved presets,
authority/compatibility failures, overflow and unload. Failure regressions cover
pre-write rejection, post-write exceptions, costume switching inside a native
callback, new-session replacement, server shutdown and unrelated expired journals.
Portable tests cover strict command parsing and atomic v14 preset decoding.
Installed-game IL checks verify factory ownership, exact native instructions,
apply/remove, SyncDictionary/stat/UI reads, native save boundaries and unload order.

Builds and tests do not run Unity's scene lifecycle or a live multiplayer session.
No deployment was performed. After manual installation, verify on/off while a
host and stock guest wear Bat, add/remove an HP-steal item, switch costumes,
reconnect and begin a second run. Compare effective character stats and actual
healing; use the unchanged costume-selection preview only as a native baseline.
