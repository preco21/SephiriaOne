# Host-only healing book and Wing-Eared Rabbit potions

Investigated 2026-09-26 against installed Sephiria 1.0.33 and HorayModAPI.
Assembly-CSharp SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
This is a feasibility investigation. No gameplay feature or deployment was added.

## Result

| Approach | Unmodified guests | Result |
| --- | --- | --- |
| A genuinely new legendary healing spell-book item | No | Guests lack its item/skill definitions and any new prefab registration. Reusing artwork does not supply the missing definitions. |
| Give an existing book additional server-side healing | Plausible with a suitable stock book | Guests retain that book's original name, description, icon and rarity. This changes an existing item and does not fulfill the requested new-item presentation. |
| Infinite ordinary healing-potion use for Wing-Eared Rabbit | Feasible from inspected native paths | Host skips consumption after a real successful drink. Native healing, controls, timing, item identity and client effects remain. No guest addon is required. |
| Share that potion healing with nearby players | Feasible as an explicit host-side extension | Apply native HP healing to eligible nearby players. This is not the costume's existing potion behavior. |

Recommended first feature: a host toggle allowing Wing-Eared Rabbit to reuse
existing HP-healing potions, optionally sharing healing nearby. Require the
player to possess at least one real potion and retain ordinary drink timing and
restrictions. Scope it to restorative HP potions initially; unlimited stat,
enchantment, dice and other consumables are a separate gameplay decision.

## Why a new legendary book requires guest definitions

`ItemDatabase.Initialize` loads each process's `Resources/Item` definitions and
then invokes local HorayModAPI registration callbacks. `NewItemOwnInstance`
contains an `EntityID`; its `Entity`, `Name` and `Context` are resolved through
that client's `ItemDatabase.FindItemById`. An unknown ID returns null. Inventory
RPCs send native item-instance data, not a complete item definition.

`ItemEntity` owns local name keys, icons, rarity and `resourcePrefab`.
`ActiveSkillEntity` separately owns a spell's local name, icon, rarity, costs,
casting metadata and magic prefab. `Charm_Magic.skill` is an ordinary prefab
reference; it is not a synchronized replacement skill definition. Stock clients
also build their spell quickslots/UI from those local components.

HorayModAPI's `RegisterNetworkPrefab` adds a prefab to the calling process's
`NetworkManager.spawnPrefabs`; it does not transmit scripts/assets to another
process. Mirror needs the corresponding prefab or spawn handler on clients.
See the [official Mirror spawn documentation](https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-spawnfunctions).
The inspected MiraItemMod reference similarly includes a client `GetPrefab`
patch for custom asset IDs; a host cannot install that patch into a stock guest.

Consequently, creating a new ID and cloning a vanilla icon/prefab on the host
does not solve guest item lookup, tooltips, rarity or spawn registration. Reusing
a stock ID can make a server behavior change available, but guests see the
original item. A host-side `rarity = Legend` edit does not update their local
rarity. A stock legendary book could retain legendary presentation, at the cost
of changing that existing book's behavior and leaving its description unchanged.

As corroboration, the author of
[Sephiria ModMaker](https://github.com/Xetsumei/Sephiria-ModMaker/blob/main/README.md)
requires matching enabled mods in multiplayer to prevent unrecognized custom
item IDs. That is the tool's policy, not a universal restriction on host-only
changes to ordinary native state.

## Potion healing and costume behavior

Ordinary restorative healing is `PotionEffect_Regeneration.CreateEffect_OnDrink`:
it calls the base potion effect, then heals a percentage of maximum HP using the
drinker’s `HpPotionBonus`. Native `UnitAvatar.HealPercent` applies healing and
hard-mode penalties, excludes dead characters, caps at maximum HP and writes
the native HP SyncVar. Calling a generic flat `Heal` alone would not reproduce
all of this potion behavior or the drink events.

There are two relevant notification paths:

1. `PotionEffect.CreateEffect_OnDrink` raises `UnitAvatar.OnDrinkPotion` through
   `ReceivePotionDrinkEvent`, when the effect's `DecreaseItemOnDrink` is true.
2. `ItemController.DrinkPotionAnimation` raises `OnDrinkPotionServerside` after
   `WieldingPotion.Drink` returns. Other native effects subscribe to these events.

The installed English localization maps `Costume_HolyRabbit_Name` to Wing-Eared
Rabbit. Its serialized costume asset identifies itself as `HolyRabbit` and
includes `PARTY_BUFF/1`, revive haste, and a weapon-damage penalty.

The native party mechanic is **buff-spell sharing**, not potion-heal sharing:
`ActiveSkill_Buff.OnStartMagic` checks `PARTYBUFF`, then applies its buff to
players within 10 world units. The localization describes this as five tiles.
`Charm_Magic.GetCost` adds 50% cost for these buff spells. An assembly string
cross-reference finds `PARTYBUFF` in these two consumers. Neither restorative
potion healing nor `HealPercent` performs this party-sharing step.

The game also contains `ActiveSkill_HealSelf`, which uses flat `Heal` and reserves
MP; it does not invoke the potion pipeline. Its Healing Stream asset is disabled
in `ActiveSkillDatabase` and uncommon, while its item (3005) is hidden and uncommon.
It is not an existing legendary potion-equivalent solution. These asset-prefix
values were checked directly after generated type-tree reading failed on some
unrelated definitions; incomplete asset parses were not treated as evidence.

## Proposed host-side implementation boundary

The normal input flow for an unmodified guest is:

```text
Existing potion input
  -> ItemController.CmdUseItemKeyDown
  -> host validates inventory, potion restrictions and action state
  -> ordinary networked potion prefab and drinking animation
  -> host DrinkPotionAnimation
  -> WieldingPotion.Drink: native potion effect and drink notification
  -> OnDrinkPotionServerside notification
  -> DecreaseItemQuantity only when itemDecreased is true
```

A narrowly scoped postfix on a successfully completed `WieldingPotion.Drink`
can set the `out bool itemDecreased` to false for an eligible host-authoritative
player. This occurs after its original effect has executed and before the
controller's quantity deduction. It avoids deleting/recreating the final potion,
changing instance IDs, overflowing stack counts, or refilling inventory every frame.

Do **not** globally change `PotionEffect.DecreaseItemOnDrink` to false: the base
effect uses that same property to decide whether to raise the potion-drink event.
Such a change could remove native potion-triggered effects. A completion hook
must also preserve the controller's later notification and cancellation behavior.

Eligibility should use the authoritative player's current `HolyRabbit` costume
and the exact held potion/instance at drink completion. Displayed names/skins
are not identities. This naturally covers costume switching, replacements,
rejoins and restarts without a cached eligible-player list. Validate the hook's
signature/IL shape and fail closed to ordinary potion consumption on unsupported
game versions. Do not broadly patch inventory quantity reductions.

If party healing is enabled, the host can select live, initialized players on the
same floor within the chosen radius and call native `HealPercent`. Reuse the
costume's 10-world-unit radius as an initial proposal. Exclude the drinker from
the additional loop, so self-healing occurs once. Recipient maximum HP and native
healing penalties must apply. The choice of source-versus-recipient potion bonus
should be explicit in the final feature design. Additional recipients should
not recursively replay a drink, consume an item, or trigger another sharing pass.

No new guest assets, scripts, custom network messages or addon policy state are
needed for this alternative. Guests use existing potion controls/prefabs and
receive ordinary native HP/inventory state. The infinity toggle and any sharing
policy stay on the host; stock tooltips would still describe the normal potion.
There is no new legendary item or custom guest UI in this approach.

Use the existing host command/panel action and preset infrastructure for toggles,
reset and status. The healing itself is a one-time event per successful drink,
not a maintained stat target: never replay it during reconciliation, re-entry or
preset loading. No new hotkey is necessary. Existing potion-use input is enough.

## Verification required before release

- Fixture tests around the real drink boundary: effect exactly once, both event
  paths preserved, final potion retained, ordinary players still consume, and
  disabled/reset policy immediately restores normal consumption.
- Eligibility changes during drinking, cancelled/blocked use, stale instance or
  quickslot changes, death/disconnect, new avatars, repeated re-entry and restart.
- HP bonuses, healing/hard-mode penalties, full HP, dead recipients, radius and
  floor separation; no duplicate/recursive party healing.
- Explicit HP-potion allowlist: ordinary, large and any intentionally supported
  sample potion. Other consumables remain native unless separately requested.
- No unchanged-tick writes, background refill, or new custom network state.
- Live host with unmodified guests: guest-initiated drinking, unchanged quantity,
  HP replication, native visuals, save/reload and default behavior after reset.

This investigation verified installed code, localization, relevant asset values,
and network boundaries. It did not execute a modified feature in Unity or a live
multiplayer session. Decompiled code, extracted data and analysis scripts remain
outside the repository.
