# Wing-Eared Rabbit potion options

Since `0.27.0`, the costume also receives [Crest of the Iron Wall as a starting
artifact](rabbit-starting-artifact.md). That costume-bound addition is independent
of the optional potion settings below.

Since `0.28.0`, a fifth independent toggle grants a [random non-HP/MP potion on
each earned level](rabbit-level-up-potions.md): `/one rabbit level-up-potion on|off`.
It is off by default, works with unmodified guests, and is saved in v10 presets
when enabled. `/one rabbit reset` also disables it.

Added in `0.16.0`, extended in `0.17.0` and `0.18.0`. All four HP-potion options are **off by default** and apply only while a
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
Only Restorative Potion (20%) and Large Restorative Potion (50%), IDs 0/1,
support these options. Since `0.39.1`, **Potion of Regeneration (Sample)** (1%,
ID 37) is excluded from every Rabbit HP-potion option: no addon MP cost,
low-MP rejection/alert, shared healing/particles, infinite use or Survival
suppression. It keeps native healing, consumption and Survival behavior, even
with all options enabled and zero MP. This extends the earlier Survival
(`0.27.2`) and consumption (`0.28.1`) exceptions to the entire healing path.

**Tension boss-combat exception (`0.29.0`):** With infinite use enabled,
Wing-Eared Rabbit can use regular/large HP potions (IDs 0/1) even while Root's
Retreat's Tension Plan Shard blocks potion use during a boss fight. This is part
of the existing infinite-use toggle; there is no separate option or preset change.
Sample (37), MP, buff and other potions remain blocked. Other costumes and Rabbit
players with infinite use off retain normal Tension behavior. MP fees, low-MP
rejection, drinking time/cancellation, death guards and Survival settings still
apply. Host-only installation covers both the host and unmodified guests.

The installed game's internal Tension key is `HOSTILITY`.
`ItemController.LocalUseItemKeyDown` runs for local use and received guest
commands. It checks the selected potion's `CanDrink`, then
`IsHostilityBlockingPotion`, before spawning a wielded potion or starting its
animation. The addon preserves this method and filters only the Tension Boolean
result, passing the exact native inventory item. It never removes the shard,
edits `hardModeEnvironment`, suppresses boss-combat state or changes guest code.

The filter reads current costume, infinite setting, live connection ownership,
selected-item identity and native regeneration effect on each attempted use. It
does not cache a per-player permission, so costume/preset changes and reconnects
take effect immediately. A changed selection or stale connection keeps the native
block. Native-shape guards reject incompatible game updates and shutdown removes
the hook with the other potion patches. There is no per-frame work or new RPC.

Verification for `0.29.0`: all 239 potion scenarios, 46 description checks,
6 localized description checks, 834 runtime checks and 48 localization checks
pass. Debug/Release builds have zero warnings or errors. The full portable and
installed-game suite passes, including the host/guest Tension entry points,
preserved native instructions and rejection of changed hook shapes. Independent
review found no actionable issues. Live boss combat remains to be checked; no
deployment was performed.

**Shared potion healing** forwards the actual native healing percentage,
including the drinker's potion bonus, to other alive, ready players on the same
floor within 5 tiles (strictly less than 10 world units). Each recipient applies
their own healing/hard-mode penalties and maximum-HP cap. Their costume does not
need to be Wing-Eared Rabbit. Shared healing does not make recipients drink a
potion, trigger their potion-use passives, consume their inventory, or relay
healing again. It does not share regeneration, all healing sources or spell heals.

Since `0.22.0`, each recipient whose HP actually increases also gets the game's
[green HP-potion particles](rabbit-shared-healing-visuals.md), visible to unmodified
guests observing that avatar. This follows the existing sharing toggle; no extra
setting is needed. Full-health/zero-healing recipients get no effect. Connection,
avatar, run and floor are rechecked after healing callbacks. Visual failures never
retry healing or interrupt MP, infinite-potion or Survival protections.

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

**Low-MP feedback (`0.19.0`):** A rejected attempt displays, for example,
`Not enough MP to heal (6/25 MP).` The numbers are the current balance and fee at
completion. The host/single-player drinker sees the native 2.5-second system
message; an unmodified guest sees native amber floating text at their character.
Only that drinker receives the alert. Successful, cancelled and already-dead
attempts do not show it, nor do other costumes/potion types or disabled/zero fees.
Each rejected attempt can notify again; there is no background low-MP polling.

The system-message event is local-only. The game's custom-message RPC feeds shop
events, so it cannot provide a generic center message to an unmodified guest.
Guest feedback uses the existing `UnitAvatar.RpcShowDamageParticle` receiver,
its exact native payload and Mirror's targeted sender on the reliable channel.
Its serializer/reader contract is checked before sending. A game update that
breaks the contract disables guest feedback with a bounded log warning; a UI or
send failure still rejects the potion. No player/connection is cached, no old
alerts are replayed on reconnect, and every pooled writer is returned on failure.

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
for other costumes and non-HP potions. Since **0.27.2**, **Potion of Regeneration
(Sample)** (`37`, Korean: 재생의 포션 (맛보기 샘플)) is also exempt: a completed drink
still runs the native Survival callback with suppression enabled. Since `0.39.1`,
Sample is excluded from all Rabbit HP-potion options, including MP cost and
shared healing, and continues to consume normally.
Other supported HP potions (`0`, `1`) retain suppression. This option also works when infinite uses
and/or sharing are off.

With suppression on, regular HP potions do not grant the Rabbit drinker Survival
stats; the Sample potion still can. Recipients of any costume are already
protected by the HP-only sharing path: `HealPercent` changes HP without emitting
`OnDrinkPotion`, the event Survival subscribes to. Receiving shared healing never
grants a Survival bonus, even when suppression is off. The toggle controls the
drinker only because only that player actually drinks. A recipient's separate
native potion use still follows its own costume/potion rules; no nearby player's
talent is globally disabled or temporarily removed.

These are independent options: sharing alone still consumes the source potion;
infinite alone affects only the drinker. Native Wing-Eared Rabbit buff-spell
sharing remains unchanged.

**Death timing (`0.18.1`):** Once an eligible drink reaches completion while its
player is alive, death during that same operation does not cancel infinite-item
protection or Survival suppression. Native death may destroy/unwield the potion
before the managed drink returns; the addon retains the exact admitted operation
without trying to resurrect the potion object or player. It still checks the same
host connection, session, costume and original inventory item. A dead source does
not start shared healing. A completed admission pays MP once, without refunds or
replay after death.

If the player is already dead when an eligible pending completion reaches the
controller guard, the addon rejects it before MP charges, potion events, healing
or item consumption. The game's normal animation cleanup still runs. This applies
only to Rabbit HP potions with at least one applicable Rabbit option enabled.
For the Sample potion, none of these options is applicable; even with all
enabled, the addon does not intercept its native completion path. All-off settings
(including a remembered disabled fee), other costumes and non-HP potions remain
native. No per-player death flag or delayed operation survives the call stack.
The shared eligibility predicate excludes Sample before creating a Rabbit drink
context. Nested drinks retain separate scopes, so an admitted regular drink
cannot apply its MP, consumption or Survival policy to a nested Sample drink.

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
close or unload. Added lines follow the addon's English/Korean language setting.

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

The `0.39.1` regression reproduces Sample MP charges and sharing left behind by
the earlier partial exclusions. One eligibility predicate now admits only IDs
0/1 for healing, retention, Survival suppression and Tension. Real Harmony
fixtures cover all 16 flag combinations, local/guest ownership, zero MP with
a maximum fee, native healing bonuses, nested drinks, last-unit consumption,
three death phases, wield teardown and late completion. The potion suite passes
255 scenarios plus 12 localized alert checks; descriptions pass 48 checks plus
10 localized checks, including share-only and MP-only EN/KO Sample exclusions.
Debug/Release builds pass with zero warnings/errors, alongside the portable
and installed-game contracts and 1,255 runtime integration checks. These are
fixture/contract results, not live gameplay verification; no deployment.

The `0.28.1` consumption regression reproduces the prior retention with infinite
use enabled. Coverage checks all option combinations, local/guest owners,
last-unit consumption, nested Sample/regular drinks, all three death phases and
wield teardown. Standard HP potions retain their infinite/suppression protection.
Debug/Release builds, installed-game contracts, 217 potion scenarios plus 12
localized alerts, 834 runtime checks, 44 description plus 6 localized checks and
the portable/catalog suite passed. No deployment or live gameplay test.

Verification for `0.27.2` on 2026-10-08: Debug/Release builds and installed-game
contracts passed, alongside 214 potion-hook scenarios, 12 localized alerts,
824 session/runtime checks, 42 description checks and the portable/catalog suite.
The Sample exception passed all 16 option combinations, nested Sample/regular
drinks, shared recipients, all three death phases and wield teardown. Independent
review found no actionable defects. No deployment or live gameplay test.

Verification for `0.18.0` on 2026-09-26: Debug and Release builds passed with zero
warnings and errors using `-p:DeployMod=false`. Four affected test runners passed
in Release: 1,122 pure checks, 682 session/runtime checks, 76 potion-hook scenarios
and 35 description checks. Potion scenarios include custom costs of 0, 1, 25 and
10000, insufficient/exact/excess balances, mid-drink fee changes, all 16 option combinations,
insufficient funds, cancellation, nested calls, unrelated passives, callback
faults and lifetime changes. Installed-game IL contracts passed, including the
native catch/cleanup path, synchronized MP setter, Survival callback, consumption
decision, single regeneration-heal call, HP replication and costume-tooltip
signatures. Modified consumer IL without the required catch, event or cleanup
is rejected. Independent review found no actionable issues. Live Unity/Mirror
and rendered panel verification remain pending.
The recipient-Survival follow-up reran all 76 potion scenarios and the pure/installed
contracts. Added coverage checks both recipient costumes, all three supported HP
potion IDs, suppression on/off, independent potion use during/after shared healing,
and recipient callback failures. Installed IL confirms HP-only sharing does not
raise drink events and Survival subscribes only to `OnDrinkPotion`. No runtime
change or additional suppression hook was needed for recipients.

The `0.18.1` death-transition repair initially reproduced five failing cases:
death before Survival, death during healing, both with and without wield cleanup,
and a late completion already dead. After repair, all **115** potion scenarios
pass, including Unity-style destroyed-object truthiness, all 16 toggle combinations,
11 concurrent lifetime/input changes, revived players and ordinary potion behavior.
Installed IL verifies death/cancellation can destroy and clear the wielded item,
and the potion event precedes regeneration healing. Debug/Release builds and the
pure/installed-game suite pass with deployment disabled. Independent review found
no critical or important issues. Session-scope inspection confirms death does not
clear shared Rabbit intent. Live lethal-hit timing
and unmodified-guest multiplayer verification remain pending.
Verification for `0.19.0` on 2026-09-28: the missing local/guest/reconnect feedback
first produced three failing fixtures. After implementation, all **134** potion
scenarios pass. Coverage includes owner-only delivery, live amounts, repeated
attempts, stale owners, reconnect, failed UI/transport, writer cleanup, silent
non-rejections and changed RPC contracts. Installed-code checks compare the
addon's serializer calls with the game's exact serializers and inspect native
receiver/targeted transport/UI subscription. Debug/Release builds passed with zero
warnings; 1,122 pure checks, 682 runtime checks and 35 description checks passed.
Independent review found no actionable issues. Deployment stayed disabled.
Live appearance and unmodified-guest
delivery still need the manual check below.

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
8. Give Rabbit and non-Rabbit players Survival 5. With suppression on, regular Rabbit HP
   potions must not grant random stats, while the Sample potion, non-Rabbit drinks,
   Rabbit non-HP potions and unrelated potion passives behave normally. Turn suppression off
   and verify Survival works again for the drinker. Nearby recipients should gain
   HP without random stats with either toggle setting. Confirm recipients' own
   non-Rabbit HP or any non-HP potion still triggers their normal Survival bonus.
   Already-earned stats must remain intact.
9. Enable every Rabbit option and drink mana/status potions. Verify ordinary
   consumption, no extra MP fee and unchanged Survival behavior. Change costume,
   leave/rejoin, return to lobby and restart to check current-intent scoping.
10. Set 25 using the Rabbit panel, then change to 7 in chat during a drink animation.
    Confirm completion charges 7. Compare current status and the host's costume
    description. Try zero, invalid input and insufficient MP. Turn charging off,
    save and restart: the chosen amount should remain, without enabling charging.
    Reenable it and test with a rejoining unmodified guest. Reset restores 10/off.
11. With infinite and suppression enabled and Survival 5 allocated, finish a regular HP
    potion immediately before a lethal hit. Verify the item remains and no random
    stat is granted. Repeat when death precedes the pending completion: no MP,
    healing, potion events or consumption should occur. Revive and drink again,
    then repeat with an unmodified guest and with each option disabled separately.
    Repeat the alive-to-dead completed drink with the Sample potion: it must retain
    the native Survival bonus and consume exactly one unit, even with infinite
    use enabled. Repeat with the final unit of the Sample stack. With every
    Rabbit option enabled, Sample must cost no MP or share healing/particles.
    Repeat at zero MP: native self-healing, consumption and Survival must still
    work, with no low-MP alert. Check both the host and an unmodified guest.
12. Set MP cost to 25 and attempt healing with less MP as host/single player and
    as an unmodified guest. Verify the balance/cost text, local timed message and
    guest floating text; other players must see neither alert. Repeat with a new
    fee, after reconnect and with enough MP. Rejected attempts must preserve the
    potion and grant neither healing nor Survival stats. No low-MP notice should
    appear after cancellation, death, turning the fee off, or setting it to zero.
13. Enable Tension and infinite use, then enter boss combat as host and with an
    unmodified Rabbit guest. Verify regular/large HP potions can be used and
    retained, including MP fees, insufficient-MP rejection and Survival settings.
    Sample, MP and buff potions must remain blocked. Switch costumes or turn
    infinite use off and confirm HP potions become blocked too. Rejoin and repeat;
    outside boss combat, ordinary potion use must remain unchanged.
