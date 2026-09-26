# Wing-Eared Rabbit potion options

Added in `0.16.0`, extended in `0.17.0` and `0.18.0`. All four options are **off by default** and apply only while a
player wears Wing-Eared Rabbit (`HolyRabbit`). The host needs the addon; guests
use the ordinary game's potion controls, inventory and HP synchronization.

```text
/one rabbit infinite on
/one rabbit share on
/one rabbit mp-cost on
/one rabbit mp-cost 25
/one rabbit suppress-survival on
/one rabbit status
/one rabbit infinite off
/one rabbit share off
/one rabbit reset
/one save
```

The **Rabbit** tab in `/one ui` exposes the same independent On/Off actions, an
MP-cost input with **Set & on**, and reset. `/one save` stores all flags and the
chosen fee for future hosted sessions. Reset affects
the current session; save again or `/one forget` to change the saved copy.
Custom MP amounts use preset v6, including amounts retained while charging is off.
Presets with the default fee and MP cost or Survival suppression use v5; original
infinite/share-only presets still use v4. v1-v5 still load with a 10 MP fee; v1-v4
leave the new toggles off. Older addon versions cannot load v6. No hotkeys were added.

## Behavior

**Infinite potions** retains the consumed stack unit after a completed drink.
Keep at least one healing potion and use it normally. Drinking time, cancellation,
native potion effects and drink-triggered passives remain intact unless Survival
suppression is explicitly enabled. It does not create or periodically refill
items. Mana, buff, status and other non-HP potions keep their native consumption
and talent behavior, even with every Rabbit option enabled. Other costumes are unchanged.
Supported native regeneration potions are Restorative Potion (20%), Large
Restorative Potion (50%) and Sample potion (1%); IDs 0, 1 and 37.

**Shared potion healing** forwards the actual native healing percentage,
including the drinker's potion bonus, to other alive, ready players on the same
floor within 5 tiles (strictly less than 10 world units). Each recipient applies
their own healing/hard-mode penalties and maximum-HP cap. Their costume does not
need to be Wing-Eared Rabbit. Shared healing does not make recipients drink a
potion, trigger their potion-use passives, consume their inventory, or relay
healing again. It does not share regeneration, all healing sources or spell heals.

**MP cost** charges a configurable flat fee, initially **10 MP**, when an eligible
HP-potion drink reaches completion. `/one rabbit mp-cost 25` sets the fee to 25 MP
and enables charging. Use whole numbers **0..10000**; zero costs no MP and does
not write to the MP balance. Negative values, fractions and `xN` are rejected.
`mp-cost off` disables charging and remembers the amount; `mp-cost on` reuses it.
`/one rabbit reset` restores 10 MP and switches all Rabbit options off.

The amount current at drink completion applies, including edits made while the
drinking animation runs. A change during the native potion callbacks only affects
the next drink. Insufficient MP rejects the entire drink: no healing,
sharing, potion events, random-stat gain or item consumption. Cancelled drinks
cost nothing. The drinking animation may run before a completion-time rejection;
native wield/animation cleanup still runs.

The fixed fee uses the game's synchronized MP value. It is separate from spell
costs: spell discounts and `INFINITYMP` do not waive it, and the addon does not
invoke native MP-spend passives. If a native potion callback fails after payment,
the fee is retained; it is never retried or refunded over unrelated MP changes.
The session's `RabbitPotionSettings.MpCostPerDrink` is shared by the native charge,
status, panel, description and preset. No per-player fee cache is needed.

**Suppress Survival bonus** skips only Survival's rank-5 random-stat-on-potion
callback for the current eligible Rabbit HP-potion drink. It does not disable
the Survival talent, remove its allocation or other effects, undo stats already
earned, or suppress other potion-use passives. Normal Survival behavior remains
for other costumes and non-HP potions. This option also works when infinite uses
and/or sharing are off.

These are independent options: sharing alone still consumes the source potion;
infinite alone affects only the drinker. Native Wing-Eared Rabbit buff-spell
sharing remains unchanged.

## Synchronization and compatibility

The host observes the existing successful potion-use event. A scoped context
validates current costume, owned player connection, selected quickslot, inventory
instance, run and session. It captures the percentage at the specific native
regeneration call, retaining the native drinker's calculation. No new item IDs,
prefabs, RPCs, client scripts or custom replicated fields are introduced.

All settings live in the shared session policy. Each completed use checks live
intent; there are no per-player inherited potion flags or healing queues. New and
rejoining players use the current settings automatically, including changed
settings while they were away. Restarts and preset loading never replay healing or MP charges.
Costume changes take effect on the next drink without an extra sync handler.

Native hook compatibility is separate from other command families. If unavailable,
enabling is blocked and potion behavior stays native; off/reset remain available.
The host panel and `/one rabbit status` report availability. Installation errors
are recorded in Player.log. Other native callback errors do not cause addon heal
retries or escape into native network-command handling.

The installed asset mapping confirms the targeted talent: `6_Survive` in
`resources.assets` (path ID 29773) points its rank-5 perk to `6_LV5_PotionRandom`
in `sharedassets0.assets` (GameObject 70629), whose component is
`PassiveObject_PotionAndRandomStat` (185562). Its `HandleDrinkPotion` callback
adds the random status and notification; skipping that callback does not alter
the general `OnDrinkPotion` or controller potion events.

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

Verification for `0.18.0` on 2026-09-26: Debug and Release builds passed with zero
warnings and errors using `-p:DeployMod=false`. Four affected test runners passed
in Release: 1,122 pure checks, 682 session/runtime checks, 61 potion-hook scenarios
and 35 description checks. Potion scenarios include custom costs of 0, 1, 25 and
10000, insufficient/exact/excess balances, mid-drink fee changes, all 16 option combinations,
insufficient funds, cancellation, nested calls, unrelated passives, callback
faults and lifetime changes. Installed-game IL contracts passed, including the
native catch/cleanup path, synchronized MP setter, Survival callback, consumption
decision, single regeneration-heal call, HP replication and costume-tooltip
signatures. Modified consumer IL without the required catch, event or cleanup
is rejected. Independent review found no actionable issues. Live Unity/Mirror
and rendered panel verification remain pending.
The `0.17.0` five-player synchronization fixture retained zero allocated bytes per tick
with both inactive and active settings. This is not a live-game profiler result.

Manual smoke tests:

1. With all flags off, drink a potion wearing HolyRabbit and another costume;
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
7. Enable MP cost only and test MP below, equal to and above 10. Insufficient MP
   must leave HP, items, potion passives and nearby players unchanged. Successful
   drinks cost exactly 10; cancelled drinking costs zero. Repeat with an unmodified
   guest and with infinite/share enabled.
8. Give Rabbit and non-Rabbit players Survival 5. With suppression on, Rabbit HP
   potions must not grant random stats, while non-Rabbit drinks, Rabbit non-HP
   potions and unrelated potion passives behave normally. Turn suppression off
   and verify Survival works again. Already-earned stats must remain intact.
9. Enable every Rabbit option and drink mana/status potions. Verify ordinary
   consumption, no extra MP fee and unchanged Survival behavior. Change costume,
   leave/rejoin, return to lobby and restart to check current-intent scoping.
10. Set 25 using the Rabbit panel, then change to 7 in chat during a drink animation.
    Confirm completion charges 7. Compare current status and the host's costume
    description. Try zero, invalid input and insufficient MP. Turn charging off,
    save and restart: the chosen amount should remain, without enabling charging.
    Reenable it and test with a rejoining unmodified guest. Reset restores 10/off.
