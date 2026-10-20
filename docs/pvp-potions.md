# PvP potion supplies and consumption

## Infinite HP potions (0.71.0; default changed in 0.72.0)

**Combat → Supplies → Infinite PvP HP potions** / `/one friendlyfire infinite-hp
on|off|reset` is a separate toggle, default **on**. `/one save` stores it in preset
v37. Explicit saved `infinite-hp 0` remains off; omitted settings inherit on.
Both per-option reset and full friendly-fire reset restore on. Earlier saves
that omitted the old off default cannot distinguish an opt-out from no preference.
It does not require PvP balancing or enable the automatic Large Potion grant.

Active combat participants of any costume retain ordinary HP potions (ID 0) and
Large Potions (ID 1) after a successful drink. Native self-healing and potion
callbacks still run. The Sample potion (ID 37), MP potions and other potion types
remain fully native. This preserves the prior Sample exclusion.

The shared live `PvpParticipation` rule covers casual friendly fire, Deathmatch,
Tournament, Play Tag and Zombie Survival. Selection, role and connection changes
take effect without a separate potion roster/cache. Countdowns and spectators,
including Tournament players outside the current pair, keep normal consumption.
The supply grant deliberately uses the broader entry roster described below.

Turning the option off or ending active PvP immediately restores native consumption
for subsequent drinks. Rabbit's original costume option remains independent and
can still retain HP potions outside PvP. Enabling the PvP option does not grant
other costumes Rabbit's MP fee, sharing, Survival suppression or Tension bypass.
The Rabbit-specific settings still apply to Rabbit normally.

### Native boundary and lifecycle

`PvpPotionPolicy` borrows the existing guarded `RabbitPotionNativeHooks` drink
scope rather than installing a second patch. The scope captures an existing
participant receipt and costume, checks connection/run/floor/inventory/item
identity, and changes only the native `itemDecreased` result after one successful
HP heal. No quantity refund, retained inventory mutation, new RPC or guest addon
is needed. The existing death-transition protection remains in use.

`WieldingPotion.Drink` calls the native effect and returns its consumption decision
before `ItemController.OnDrinkPotionServerside`. Changes inside the effect/heal
callback are rechecked before that decision. A completed drink is atomic: a later
controller notification cannot retroactively consume it; an option/mode change
there affects the next drink. Regression coverage asserts both sides of this
boundary, plus stale connections, costume changes, dead/late completions,
non-Rabbit isolation, all shared modes and live toggle changes.

Verified with a non-deploying Release build (zero warnings/errors), 301 potion
hook cases plus 12 localized alerts, 96 new potion setting/preset checks, shared
runtime/combat suites and installed native contracts. Live host/stock-guest
drinking and belt-count rendering remain unverified; nothing was deployed.

## Large Potion supplies (0.70.0)

Host controls under **Combat → Supplies**:

- **Give Large Potion to all** / `/one friendlyfire potion`: one native Large
  Potion per current player who lacks one, including spectators. Works with
  friendly fire off. Reports granted, already owned, loading and failed counts.
- **Auto Large Potion** / `/one friendlyfire auto-potion on|off|reset`: default
  **off**, independent of PvP balancing. `/one save` persists it (preset v36).

Automatic supply checks happen on casual PvP entry, joining the active selection,
new connections, and each new shared match. Deathmatch, Tournament, Play Tag and
Zombie Survival use the same path. No selection means everyone; otherwise only
selected entrants qualify. Countdown participants receive supplies before fighting;
Tournament includes its waiting roster, not just the current pair. Late entrants
must satisfy the mode's existing roster rules. Run/session initialization gets a
fresh receipt and ownership check.

This is an entry grant, not an infinite-potion policy. Consumption, death/respawn,
floor loading, K/D/A resets, unrelated tuning and later rounds of the same match
do not refill it. Turning the option on during active PvP checks the current
participants once. Turning it off or ending PvP stops future grants; already
granted potions remain ordinary native items.

## Native findings

Installed `Assembly-CSharp.dll` SHA256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.

The asset audit identifies **item ID 1**, `0001_BigPotionOfRegeneration`, in
`Sephiria_Data/resources.assets`, path ID **28856**. Its localization key is
`Item_BigPotionOfRegeneration_Name`, type Potion, active type enabled. Its prefab
reference points to sharedassets0 path 62401. Existing HP-potion research confirms
ID 1 is the large restorative potion; small ID 0 and Sample ID 37 are distinct.
Runtime validation checks ID/type/enabled/name key at database load and again
before every actual grant, including addon replacements after load.

Ownership checks positive quantities in all native carried stores:

- `GridInventory.inventoryMatrix`: normal grid and potion belt (y = 100), fields
  `NewItemOwnInstance.EntityID` / `Quantity`.
- `subBagMatrix`: `ItemMetadata.entityID` / `quantity`.
- `temporaryInventory`: pending native overflow, with the same metadata.

Native `ItemDatabase.GenerateInstanceID(Random)` and
`GridInventory.LocalAddItem(instance, 1, 1, 0, true, false)` own placement,
stacking, native notification and Mirror inventory replication. A definite
failure with still-current scope/permission and no owned potion gets one
`temporaryInventory` entry, following native overflow behavior. This is not
registered as a costume starting item. Existing permission is borrowed because
`GridInventory.Permission` is not reentrant. Exceptions never trigger overflow
or automatic retries: a callback may already have committed an item.

No custom item, asset, guest addon or custom RPC is involved. Server changes use
native synchronized dictionaries/lists understood by unmodified guests. Overflow
follows the game's temporary-item UI/lifetime rather than guaranteed save storage.

## Shared lifecycle and performance

`PvpPotionSupplies` uses the shared typed `ReconciliationCoordinator` and
`FriendlyFireParticipants.Receipt`. The independent optional coordinator cannot
block critical stats/Fountain reconciliation, and their failures cannot block it.
The common session controller drives it after match lifecycle processing. Match
roster eligibility is centralized in `CombatMatchRuntime.IsSupplyParticipant`.

Keys observe activation revision, shared match token and eligibility, never
inventory quantities. First delivery can wait for native readiness. Readiness is
deliberately checked inside delivery, not as the coordinator rule's readiness
predicate: otherwise a later loading frame would turn an Applied/Faulted cursor
back into Waiting and replay a consumed or ambiguous grant. Departed receipts and
session shutdown clear owned cursors. Scratch collections are reused; stable
nine-player fixture ticks allocate zero bytes and never scan inventories.

Manual batch snapshots and per-write identity/run/inventory guards reject stale
targets, nested actions and connection replacement. Existing stat planning and
match damage/respawn behavior are unchanged.

## Verification / remaining live check

Runtime fixtures cover nine players, all stores, full inventory, repeat actions,
selection changes, all four modes and countdown rosters, loading/rejoining,
transient readiness, consumed items, partial exceptions, reentry, preset migration
and stable-frame allocations. Installed-game IL contracts verify native add,
permission and stock-client inventory synchronization. English/Korean labels and
commands share the standard localization catalog.

Verified: Release build with zero warnings/errors, 3,490 runtime checks, 6,120
friendly-fire hook checks, 40 addon lifecycle checks, 3,999 catalog checks, panel
tests and installed-game contracts. No deployment or game launch was performed.

Not deployed. Live host/stock-guest verification remains: grant from lobby/run,
confirm the item/belt notification and overflow UI, consume it, change floor,
start each match and reconnect. Fixture/IL checks do not prove rendered guest UI.
