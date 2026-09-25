# Wing-Eared Rabbit potion options

Added in `0.16.0`. Both options are **off by default** and apply only while a
player wears Wing-Eared Rabbit (`HolyRabbit`). The host needs the addon; guests
use the ordinary game's potion controls, inventory and HP synchronization.

```text
/one rabbit infinite on
/one rabbit share on
/one rabbit status
/one rabbit infinite off
/one rabbit share off
/one rabbit reset
/one save
```

The **Rabbit** tab in `/one ui` exposes the same independent On/Off actions and
reset. `/one save` stores both flags for future hosted sessions. Reset affects
the current session; save again or `/one forget` to change the saved copy.
Presets with enabled rabbit options use v4; v1-v3 still load. Older addon versions
cannot load v4. No hotkeys were added.

## Behavior

**Infinite potions** retains the consumed stack unit after a completed drink.
Keep at least one healing potion and use it normally. Drinking time, cancellation,
native potion effects and drink-triggered passives remain intact. It does not
create or periodically refill items. Other consumables and costumes are unchanged.
Supported native regeneration potions are Restorative Potion (20%), Large
Restorative Potion (50%) and Sample potion (1%); IDs 0, 1 and 37.

**Shared potion healing** forwards the actual native healing percentage,
including the drinker's potion bonus, to other alive, ready players on the same
floor within 5 tiles (strictly less than 10 world units). Each recipient applies
their own healing/hard-mode penalties and maximum-HP cap. Their costume does not
need to be Wing-Eared Rabbit. Shared healing does not make recipients drink a
potion, trigger their potion-use passives, consume their inventory, or relay
healing again. It does not share regeneration, all healing sources or spell heals.

These are independent options: sharing alone still consumes the source potion;
infinite alone affects only the drinker. Native Wing-Eared Rabbit buff-spell
sharing remains unchanged.

## Synchronization and compatibility

The host observes the existing successful potion-use event. A scoped context
validates current costume, owned player connection, selected quickslot, inventory
instance, run and session. It captures the percentage at the specific native
regeneration call, retaining the native drinker's calculation. No new item IDs,
prefabs, RPCs, client scripts or custom replicated fields are introduced.

Both settings live in the shared session policy. Each completed use checks live
intent; there are no per-player inherited potion flags or healing queues. New and
rejoining players use the current settings automatically, including changed
settings while they were away. Restarts and preset loading never replay healing.
Costume changes take effect on the next drink without an extra sync handler.

Native hook compatibility is separate from other command families. If unavailable,
enabling is blocked and potion behavior stays native; off/reset remain available.
The host panel and `/one rabbit status` report availability. Installation errors
are recorded in Player.log. Other native callback errors do not cause addon heal
retries or escape into native network-command handling.

## Costume description

The host's costume screen appends a line for each enabled option to the normal
Wing-Eared Rabbit effect description. The shared settings-change notification
refreshes the displayed text after policy commits, scope resets and preset loads.
Native costume selection refreshes are also covered. The adapter modifies rendered
text only, preserves native localized effects, and removes its lines on reset,
close or unload. Added lines are English.

**Unmodified guests retain their native costume description.** That text is built
locally and has no host-controlled replication path. Gameplay still uses the
host's options. Description compatibility failure does not disable potion hooks.

## Verification

Portable command/preset tests, session integration tests and real Harmony tests
over native-shaped potion and UI fixtures cover default/off behavior, independent
flags, malformed presets, session changes, reconnect policy, native drink events,
nested/failed calls, recipient filtering, cleanup and UI refresh. Installed-game
signature/IL checks and builds target Sephiria 1.0.33; see the assembly fingerprint
in [the investigation](healing-item-investigation.md). These do not replace live
multiplayer or UI verification. Development builds are not automatically deployed.

Verification on 2026-09-26: Debug and Release builds passed with zero warnings
and errors using `-p:DeployMod=false`. All seven test runners passed: 1,050 pure
checks, 673 session/runtime checks, 71 starting-resource checks, 66 budget/inventory
checks, 14 disconnect checks, 14 potion-hook scenarios and 27 description checks.
Installed-game IL contracts passed, including the native consumption decision,
single regeneration-heal call, HP replication and costume-tooltip signatures.
The five-player synchronization fixture retained zero allocated bytes per tick
with both inactive and active settings. This is not a live-game profiler result.

Manual smoke tests:

1. With both flags off, drink a potion wearing HolyRabbit and another costume;
   verify ordinary consumption and native effects.
2. Enable infinite only. Finish two drinks from a single potion; cancel another.
   Confirm the potion remains and native drink passives still trigger only for
   successful drinks. Change costume and verify consumption returns.
3. Enable share only. Compare injured guests inside/outside 5 tiles, on another
   floor and dead; verify source consumption, recipient HP caps and penalties.
4. Enable both. Test a normal and large potion, a full-health drinker, multiple
   nearby players and other Rabbit wearers. Confirm no healing relay/double heal.
5. Change settings while a guest is disconnected; fully quit and rejoin, then
   drink. Repeat across lobby return and a new run. Joining alone must not heal.
6. Save, restart the application and host again. Inspect `/one rabbit status` and
   enabled tooltip lines; reset and ensure the lines disappear. Guests' costume
   descriptions should remain native.
