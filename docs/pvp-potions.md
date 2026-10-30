# PvP potion supplies and consumption

## Defaults update (0.102.3, 2026-10-31)

Infinite PvP HP potions now default **off**. Per-option reset and full
friendly-fire reset restore off. Explicit saved `infinite-hp 0`/`1` choices
remain respected; omitted settings inherit off. Enabled preferences serialize
as `friendlyfire infinite-hp 1` using the existing v37 schema.
Auto Large Potion remains enabled by default and independent of consumption.

## Zombie use restriction (0.97.0)

Zombie Survival's default-on `/one zombie block-hp` is a separate **admission**
rule for current Zombies: since 0.98.2, Small/Large HP potions are blocked only
when the current Zombie population exceeds the match's captured starting count,
including dead/pending Zombies. At or below that count, normal PvP potion rules
apply. Completion rechecks the population, including growth during an animation.
Since 0.98.1, Sample is excluded and
retains native use/consumption/talent behavior. Humans retain the consumption rules
below. Entry/respawn supplies remain independent and never grant use permission.
See [implementation, lifecycle and verification](zombie-survival.md#zombie-hp-potion-restriction-0970).

## Authoritative PvP consumption (0.96.2, 2026-10-27)

**Infinite PvP HP potions** / `/one friendlyfire infinite-hp on|off|reset`
is now the only consumption rule for active participants, including Wing-Eared
Rabbit. PvP balancing does not need to be enabled.

| PvP infinite HP | Small/Large HP potion use by an active participant |
| --- | --- |
| On | Retained, normal self-healing, no addon MP fee, Survival random-stat bonus suppressed |
| Off | Consumed normally, even when Rabbit's costume infinite option is on |

The previous independent `block-rabbit-infinite off` setting was a real bypass:
it admitted Rabbit retention, and the completion hook combined that with PvP
retention using OR. A source-linked native-drink regression reproduced the
conflict for both Small/Large potions, host/guest actors and balancing on/off.

`PvpPotionPolicy.RuleFor` now resolves one of Costume, PvpFinite or PvpInfinite
using current shared combat participation. `RetainsPotion` exclusively selects
that path; the costume branch is unreachable during active PvP. Capture records
the admitted rule, and completion rechecks live policy. A finite or PvP drink
cannot become costume-infinite because a callback ends the match or excludes its
participant. Infinite PvP still requires the validated native completion and
current connection receipt; existing death, nested-drink, item/slot, floor,
initialization and Survival-revocation guards remain in place.

The redundant Rabbit override checkbox and stored gameplay setting are removed.
Old `block-rabbit-infinite on|off|reset` commands return a localized explanation
without mutating policy, scores, matches or supply state. Valid v53 preset rows
are accepted and ignored, then omitted on the next save; malformed/duplicate rows
still reject the preset atomically. General infinite and automatic-grant choices
survive migration independently. No new preset format is needed.

**Automatic grants are separate.** `PvpPotionSupplies` and `LargePotionGrant` are
unchanged: Auto Large Potion checks eligible players at session/match entry and
after successful respawn, and grants one only when missing. Changing consumption
policy does not refill a used potion. The manual grant button/command also stays
unchanged. No inventory snapshot restoration, quantity refund, custom item or
additional network message is introduced.

Outside active combat participation, Rabbit's costume behavior resumes. The
existing exclusions for selection/countdown/Tournament spectators remain; Small
and Large are IDs 0/1, while Sample ID 37 and other potions remain excluded from
these overrides. Finite drinks retain independently configured Rabbit MP fees,
Survival suppression and sharing. Rabbit's costume-infinite Tension exception
cannot bypass PvP policy; general PvP infinite still does not exempt Tension.
EN/KO UI, help, status, tooltip and role notices describe the same precedence.

Verification: 393 source-linked potion-hook cases plus 12 localized alerts,
14,448 runtime/session checks (including supplies, all four match modes and
rejoins), 208 legacy preset/command checks, 53 tooltip plus 12 translated-tooltip
checks, panel/localization suites, the full installed-game contract suite and
41 HarmonyX API references passed. Release build has zero warnings/errors.
Independent code review found no actionable issues. Live gameplay remains
unverified; no deployment was performed.

## Rabbit costume override (historical 0.94.0)

Superseded by 0.96.2 above: the optional bypass and its checkbox no longer exist.
The following records the original investigation and migration context.

**Combat → Block Rabbit infinite HP in PvP** (Korean: **PvP 토끼 HP 포션 무한 사용 차단**)
is enabled by default. Command:

```text
/one friendlyfire block-rabbit-infinite on|off|reset
```

The original bug was an independent OR in `CompleteDrink`: Rabbit's costume
infinite option retained the potion even with general PvP infinite HP off.
The new control restricts that costume branch for active participants:

| General PvP infinite | Block Rabbit infinite in PvP | Rabbit infinite | Result |
| --- | --- | --- | --- |
| On | Either | Either | Small/Large HP retained, no addon MP fee, Survival suppressed |
| Off | On (default) | Either | Native HP potion consumption |
| Off | Off | On | Costume infinite behavior |
| Off | Off | Off | Native HP potion consumption |

This does not require PvP balancing. Casual friendly fire, Deathmatch,
Tournament, Play Tag and Zombie Survival share the same live participant rule;
selection exclusions, countdowns and Tournament spectators stay outside combat
admission. Rejoining avatars get fresh connection admission without stored
potion eligibility. Setting changes are immediate and do not stop a match,
reset K/D/A or grant supplies.

The native guarded drink captures whether Rabbit retention was allowed, then
rechecks the live restriction at completion. Ending PvP or disabling the
restriction during callbacks cannot retroactively make a finite drink free;
enabling the restriction before completion blocks retention. The existing exact
connection/run/floor/item/slot and admitted death-transition checks still apply.
The associated costume-infinite Tension exception is also blocked in PvP.
General PvP infinite HP does not itself add a Tension exemption.

Other Rabbit settings (MP fee, Survival suppression, sharing) remain independent.
Sample ID 37 remains excluded from all HP-potion overrides. No extra network
message, spawned state, per-frame work or inventory scan is introduced.

**No count restoration:** intentionally use the user's permitted fallback.
Native consumption synchronizes the remaining inventory to stock clients.
Restoring a pre-match snapshot could duplicate or erase potions picked up,
granted, traded or used during the match. On exit, only the costume ability
resumes; spent potions stay spent. The existing grant button remains available.

`/one save` persists an opt-out in preset v53 as
`friendlyfire block-rabbit-infinite 0`. Older/omitted settings inherit on;
option reset and full friendly-fire reset restore on. EN/KO help, status,
open costume tooltip and private role notices disclose the restriction.

Verification includes native-drink host/guest fixtures for Small/Large/Sample,
general/costume precedence, balancing on/off, death and mid-drink policy changes;
real shared-mode runtime/selection/rejoin tests; v1–v52 migration and v53 roundtrip;
description/localization and installed-game contracts. Live multiplayer remains
unverified; no deployment was performed.

Verified Release build (zero warnings/errors), 392 potion-hook cases, 14,344
runtime checks, 15,728 friendly-fire hook checks, 53 description checks plus
12 translated description cases, 50 localization checks and 28 panel checks.
The full installed-game contract suite passes against the current game and
41 compiled Harmony API references. Independent review found and resolved
footer overlap and default-on checkbox recovery direction.


## Infinite HP potions (0.71.0; defaults updated through 0.102.3)

Corrected in `0.77.1` (2026-10-21): PvP retention, zero MP cost and suppression
of Survival's 5-point random-stat bonus now use the same guarded drink policy.
Previously only retention used PvP eligibility; Survival and MP still depended
on the independent Rabbit settings.

**Combat → Supplies → Infinite PvP HP potions** / `/one friendlyfire infinite-hp
on|off|reset` is a separate toggle, default **off** since 0.102.3. `/one save`
stores nondefault preferences in preset v37. Explicit saved choices remain
respected; omitted settings inherit off. Both per-option reset and full
friendly-fire reset restore off. Earlier saves that omitted the former on
default cannot distinguish an opt-in from no preference.
It does not require PvP balancing or enable the automatic Large Potion grant.

Active combat participants of any costume retain ordinary HP potions (ID 0) and
Large Potions (ID 1) after a successful drink, pay no addon MP fee (including
Wing-Eared Rabbit), and do not gain Survival's random stat for that use.
Native self-healing and unrelated potion callbacks still run. The Sample potion
(ID 37), MP potions and other potion types
remain fully native. This preserves the prior Sample exclusion.

The shared live `PvpParticipation` rule covers casual friendly fire, Deathmatch,
Tournament, Play Tag and Zombie Survival. Selection, role and connection changes
take effect without a separate potion roster/cache. Countdowns and spectators,
including Tournament players outside the current pair, keep normal consumption.
The supply grant deliberately uses the broader entry roster described below.

Turning the general option off restores native consumption for participants when
the default Rabbit override above is on. Rabbit's original costume option
can still retain HP potions outside PvP. With the PvP option active, the MP fee
is waived and Survival is suppressed regardless of the Rabbit-specific fee and
suppression toggles. When PvP ends or the option is disabled, subsequent drinks
use the costume's configured behavior, subject to the PvP override above. Sharing
and Tension remain separate Rabbit mechanics; other costumes do not acquire them through this option.

### Native boundary and lifecycle

`PvpPotionPolicy` borrows the existing guarded `RabbitPotionNativeHooks` drink
scope rather than installing a second patch. The scope captures an existing
participant receipt and costume, checks connection/run/floor/inventory/item
identity, and changes the native `itemDecreased` result after one successful HP
heal. The existing targeted Survival callback guard suppresses only the exact
player/effect in that controller scope. The fee waiver is decided at completion
entry, before any potion callback, with no later compensating MP write. No
quantity refund, retained inventory mutation, new RPC or guest addon is needed.
The existing death-transition protection remains in use.

`WieldingPotion.Drink` calls the native effect and returns its consumption decision
before `ItemController.OnDrinkPotionServerside`. Changes inside the effect/heal
callback are rechecked before that decision. A completed drink is atomic: a later
controller notification cannot retroactively consume it; an option/mode change
there affects the next drink. Regression coverage asserts both sides of this
boundary, plus stale connections, costume changes, dead/late completions,
non-Rabbit isolation, all shared modes and live toggle changes. Standalone or
nested direct `WieldingPotion.Drink` calls cannot borrow a controller's PvP
retention/suppression scope. If live policy or item/player validation permits
Survival to run, retire that drink's PvP receipt so a later callback cannot
restore free PvP consumption. This also covers temporary costume, floor, slot
or readiness changes restored during healing.

Historical `0.71.0` verification: non-deploying Release build, 301 potion
hook cases plus 12 localized alerts, 96 new potion setting/preset checks, shared
runtime/combat suites and installed native contracts. `0.77.1` adds Survival,
zero-MP Rabbit, callback revocation and nested-event regressions; the dated
implementation plan records current results. Live host/stock-guest
drinking and belt-count rendering remain unverified; nothing was deployed.

## Large Potion supplies (updated 0.94.1)

Host controls under **Combat → Supplies**:

- **Give Large Potion to all** / `/one friendlyfire potion`: one native Large
  Potion per current player who lacks one, including spectators. Works with
  friendly fire off. Reports granted, already owned, loading and failed counts.
- **Auto Large Potion** / `/one friendlyfire auto-potion on|off|reset`: default
  **on**, independent of PvP balancing. `/one save` persists an explicit opt-out
  as `friendlyfire auto-potion 0` (existing preset v36 schema). Reset restores on.
  Older presets with an omitted row inherit the new default; explicit 0/1 rows
  keep their saved value.

Automatic supply checks happen on casual PvP entry, joining the active selection,
new connections, and each new shared match. Deathmatch, Tournament, Play Tag and
Zombie Survival use the same path. No selection means everyone; otherwise only
selected entrants qualify. Countdown participants receive supplies before fighting;
Tournament includes its waiting roster, not just the current pair. Late entrants
must satisfy the mode's existing roster rules. Run/session initialization gets a
fresh receipt and ownership check.

Each successful native respawn also requests one ownership check for that
participant. If they still have a Large Potion in any carried store, nothing is
added. If it was consumed, grant one replacement through the same native path.
This includes automatic countdown revivals, native revivals and Revive All during
casual PvP, independently of the infinite-potion option. Death alone, failed or
no-op revivals, consumption while alive, floor loading, K/D/A resets and unrelated
tuning do not refill it. Turning the option on during active PvP checks the current
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
inventory quantities. The shared `UnitAvatar.Revive` prefix captures connection,
supply revision and match token; its finalizer invalidates the existing supply
cursor only after successful revival. The regular supply tick performs the
inventory work after native revival callbacks. Beginning a newer revival
acknowledges/cancels any previous life's pending supply check, including a waiting
entry check. Native revival can clear IsDead before throwing, so that partial
state must not release the previous request. Failed/nested no-op revivals,
retired connections, expired matches and disabled options cannot request supplies.
If the player dies again before delivery, another successful revival must trigger
the check. Mode cleanup disables eligibility before recovering pending players.
Each revival also has a receipt-scoped attempt ID. Older outer callbacks cannot
complete a newer nested attempt, and inventory permission callbacks cannot carry
an in-flight grant into a replacement life. Cancelled attempts are acknowledged
instead of retried as readiness failures. Attempt records are pruned with the
connection and cleared with session supply state; stable ticks do not query them.

First delivery can wait for native readiness. Readiness is
deliberately checked inside delivery, not as the coordinator rule's readiness
predicate: otherwise a later loading frame would turn an Applied/Faulted cursor
back into Waiting and replay a consumed or ambiguous grant. Departed receipts and
session shutdown clear owned cursors. Scratch collections are reused; stable
nine-player fixture ticks allocate zero bytes and never scan inventories.

Manual batch snapshots and per-write identity/run/inventory guards reject stale
targets, nested actions and connection replacement. Existing stat planning and
match damage/respawn behavior are unchanged.

### 0.94.1 verification

Runtime regressions exercise consumed and retained potions, all three stores,
manual Revive All, all four timed modes, native callback exceptions, nested
no-op revival, a second death before delivery, waiting inventories, ambiguous
grant failures, option changes and rejoining connections. Default-on/reset and
explicit-off preset round-trips are covered. Stable nine-player supply checks
still allocate zero bytes over 1,000 warmed fixture ticks, with no additional
inventory writes. Live host/guest respawn rendering remains unverified; do not
infer a live multiplayer test from fixtures or installed-game IL contracts.

Release validation: zero build warnings/errors, 14,419 runtime checks, 15,728
combat hook checks, 392 potion-hook cases, 50 localization checks, 28 panel
checks, pure policy/catalog suites and installed-game native contracts passed.
Independent review verified the nested/in-flight lifetime guards. Nothing deployed.

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
