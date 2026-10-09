# Sephiria domain findings

Consolidated 2026-10-09 (Asia/Seoul), source through addon `0.38.0`.
This summarizes reusable findings; linked feature documents contain detailed
symbols, tests and limitations. It is not a new live gameplay audit.

Most native research used installed Sephiria `1.0.33`. On this date, the local
`Assembly-CSharp.dll` SHA-256 was rechecked and still matched:

```text
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
```

A different game build requires renewed contract/asset checks. Start with the
[general guide](general-findings.md) for tooling and [index](README.md) for details.

## State lifetimes and multiplayer

The project retains gameplay effects usable by unmodified guests through native
state, existing items/prefabs and native RPC consumers. Local settings UI,
translation, diagnostics and update controls support those features; they do not
promise to alter guest UI definitions.

| Lifetime/boundary | Finding and consequence |
| --- | --- |
| Hosted session | Owns current intent. A fresh run in the same session can retain intent; a new session resolves a new scope and explicitly saved preset. |
| Run/save and dungeon | Restart can reuse avatars/dungeon while clearing native dictionaries. Track run generation/save identity; avatar presence alone cannot prove initialization is current. |
| Avatar/connection | Re-entry can reuse IDs with different object lifetimes, even before a cleanup frame. Enroll the new ready lifetime against complete current intent. |
| Durable resource checkpoint | Proves previously applied contributions, not current desired settings. Retain its applied command identity and native baseline separately. |
| Native event | Check current authority, live ownership, participants and policy at the event. Do not cache a permission forever or replay a drink/grant on join. |
| Generated offers/floors | Current policy affects the next supported generation boundary. Already generated or saved outcomes are not rerolled by a settings refresh. |

Equipment, costume, preset, passive-menu, buff and spent Root's Retreat point
changes feed the raw/bonus/amplifier inputs observed by stat maintenance. Native
cache clears still need invalidation, and consumers that run in the same frame
need `BeforeNativeRead`/`EnsureFresh`. Checkpoints for inventory/talents must be
restored before native item enumeration/allocation clamping, not after ordinary
player readiness. See [re-entry](session-reentry-review.md) and
[synchronization](synchronization-guide.md).

## Stats, Fountain, choices and resources

- **Stats:** separate native raw value from the addon's exact owned contribution.
  Relative `+10`, then `+5`, means each character's native displayed value +15.
  A delta after Set or xN starts a new native offset. `x3` replaces intent with
  native baseline ×3; repeating it does not compound. Native bonuses/amplifiers
  can change, so maintained settings recalculate against those inputs.
- **Native penalties:** a well-formed multiplier with an invalid/unrepresentable
  target restores that player's exact native value and removes only its owned
  contribution. It must not clamp a native penalty to zero or apply an unsafe
  negative multiplier result. Keep intent for later compatible native changes.
  Proven native fallback remains fresh for other features. Earlier treatment as
  globally unready caused misleading Fountain warnings and blocked inheritance.
  The warning/disconnect correlation did not establish a transport root cause.
- **Visible stats:** expose only the audited `C` menu allowlist in `StatCatalog`,
  including conditional rows. Display units matter: some percentages have a
  native 100% offset; life/MP steal are points (1 = 0.1%). A zero baseline ×N
  stays zero. A website's internal-status list is not evidence of menu visibility.
- **Fountain:** inventory allowance and dungeon `DIMENSIONPOCKETLIMIT` are separate
  values. Native grant uses the smaller. Restart clears the dungeon constants
  while avatar allowance may survive; cap-only reconciliation must run before
  granting items, without replaying the original point delta. Preserve independent
  native/other-addon limits and exact cap ownership.
- **Choices:** values are extra candidates, not total offer size. Weapon choices
  affect anvil upgrades. The native bonus baseline is normally zero and generator
  totals differ, so `/choices` deliberately does not accept xN. Existing offers
  remain unchanged; native eligible content also limits the final count.
- **Starting dice/leaves:** configure future fresh starting grants, not recurring
  wallet refills. Saved zero is a real balance. Leaves have a seed and departure
  remainder; the outstanding first-departure remainder uses the latest intent,
  even if set immediately before leaving. Completed grants never refill on
  reconnect. A decrease below the already-paid seed cannot confiscate spending;
  the documented native-allowance fallback applies.
- **Slots/talents/fruit:** total capacity/budget differs from unspent points or
  already-consumed benefits. Shrinks must preserve occupied slots and allocations.
  Revalidate before each destructive write and recovery, not only at command
  planning. Restore saved capacity early; reconcile newer/reset intent afterward.

Evidence: [relative arithmetic](relative-stat-consistency.md), [multipliers](multiplier-command.md),
[penalty review](penalty-stat-sync-review.md), [visible stats](visible-stat-modifiers.md),
[Fountain restart](fountain-run-restart.md), [choices](choice-command.md),
[resource implementation](resource-settings-implementation.md), [leaves fix](leaves-departure-fix.md).

## Native presentation and content definitions

Character names and platform lobby names are separate sources. The gradient
publishes the owned runtime name through native `playerNameSource` while in
multiplayer. Stock renderers decide whether/when to render its rich text. Steam/EOS
lobby status and member names remain platform names. The old local label registry
was removed; do not recreate it as a claim of host-only guest coverage. Replaying
`OnOpened` to refresh a panel can regenerate offers or otherwise mutate gameplay.

A genuinely new skill-book ID requires guest item/skill definitions and potentially
registered assets. Native item-instance serialization sends an ID, not its full
definition. Reusing an icon or calling `RegisterNetworkPrefab` on the host does
not install guest code. Altering the behavior of an existing native item may be
host-side, while its guest name/rarity/description remain native.

Evidence: [presentation limits](presentation-compatibility.md),
[healing-book investigation](healing-item-investigation.md).

## Costume effects and item ownership

Wing-Eared Rabbit is native `HolyRabbit`. Its vanilla `PARTYBUFF` shares buff
spells, not potion healing. Addon healing is an explicit event-driven extension
of native restorative potion behavior, with native HP replication and recipient
healing penalties/caps. Sharing does not make recipients drink or retrigger
potion-use passives. Recipients whose HP increases receive native
`UnitAvatar.RpcBloodFestivalHealFx()` green particles; a visual failure must not
repeat healing.

Only regular/large HP potions (IDs 0/1) enter Rabbit healing scopes. Since
`0.39.1`, Regeneration Sample (37) stays entirely native: normal consumption,
self-healing and Survival, without addon MP cost, low-MP blocking/alerts or
shared healing/particles. Keep eligibility shared across the healing, retention,
Survival and Tension paths; partial exclusions previously left MP charges active.
For eligible potions, MP is checked/charged at completion using current policy.
Death/callback timing requires
scoped protection through completion, not a later inventory refund. Tension's
internal key is `HOSTILITY`; only infinite Rabbit regular/large HP potion use
bypasses its boss-combat block. Other potion types/costumes keep native rules.
Read [Rabbit options and death handling](rabbit-potions.md) before changing this path.

Rabbit's Iron Wall and optional Mole/Farmer Squirrel/Turtle Collin are native
costume-bound starting grants. Preserve exact grant receipts through restart,
restock, transfer and costume removal; an item of the same type is not necessarily
the granted instance. Bat's optional 5→1 HP-steal change owns only the costume's
contribution, with paired status/stat readback so later native removal subtracts
the correct amount.

Given items use synchronized `OwnRestriction=StartingItem` and `Bound`. Fountain
items use `Bound` but already permit sales. Unlocking must project the real native
instance metadata, and unlocked ground drops must not remain guest-owned and be
destroyed on that guest's disconnect. Local `cannotThrow` item definitions such
as curse tablets remain a stock-client limitation. Reset restores recorded flags,
not completed trades or sales.

Evidence: [Rabbit artifact](rabbit-starting-artifact.md), [Collin](collin-starting-artifact.md),
[Bat](wingless-bat.md), [restrictions](item-restrictions.md),
[audited level-up potion catalog](rabbit-level-up-potions.md).

## Spawning and floor identity

Wandering, Papyrus and Taz have independent toggles, rates, first-stage conditions,
caps and optional once-per-run guarantees. A guarantee is randomly scheduled and
separate from chance extras. Multiple types can appear on one eligible floor.
Run receipts/consumed rolls are separate from preset intent: changing settings,
reloading presets or guest re-entry must not reroll/reset them.

Main dungeon stage ordinal is not map-visit count. Several maps can be in stage 1;
`from 2` excludes all of them. Merchant base-HP factors by stage are 1/2/4/5/7/8
(×8 thereafter, ×1 for unknown progress), before native stage/co-op bonuses applied
once. Existing actors retain spawn-time health. Crime exemption belongs only to
the exact addon-spawned actors, not natural merchants. Guarantees remain subject
to eligible normal rooms, safe placement and remaining route; an early-ended run
cannot always realize one. See [merchant variants](merchant-variants.md).

Mystic Jar overrides change native random locations only, preserving hidden-room
rewards, guaranteed placements and chapter gates. Random-event multipliers change
cumulative optional-room thresholds during chapter floor-data generation, not
individual room-type weights or native merchant/traveler schedules. A not-yet-
visited floor may already be generated. Preserve native seeds, RNG calls and
saved results; reconnect never rolls again. Exact-zero draws need explicit care
when a native comparison is inclusive. See [jars](mystic-jar.md) and
[event rooms](random-events.md).

## Combat and disconnect interpretation

Allowing allied collision alone did not enable HP damage: the native player's
team-protection callback vetoed it after guard MP could already be spent. Friendly
fire therefore scopes admission through the matching native callback while
retaining other vetoes, defense, shields, invulnerability and normal death.
Scale damage once at the audited boundary without mutating reusable damage input.

Companions read live leader/policy and never attack their owner. Off/reset/0%
blocks in-flight allied hits immediately; the next native AI update releases an
invalid player target. Reflection permits only audited native effects along the
exact reverse of an admitted hit, with the same damage scale and no reflection
chain. Preserve hostile targeting and unrelated procs. See [combat](friendly-fire.md).

One concrete disconnect risk was a recognized talent-budget rejection escaping a
Mirror command handler. Its targeted containment is distinct from initialization,
where unsafe saved allocations must still stop loading. Host logs also showed
peer closes, a timeout and queued-send-limit errors; these did not prove a single
addon or stat cause. Do not suppress arbitrary exceptions or weaken state guards
to hide disconnect symptoms. See [investigation](disconnect-investigation.md) and
[penalty/transport review](penalty-stat-sync-review.md).
