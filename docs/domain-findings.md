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

Private stock chat normalizes to 120 characters. Pack localized clauses before
dispatch to avoid silently losing later settings. Read-only role notices can
share session subjects and connection receipts while observing actual applied
stat contributions after maintenance; retained intent alone must not advertise
a suspended multiplier or a future costume grant. Native targeted chat uses a
null avatar so combat bubbles survive. See [costume role notices](costume-role-notices.md).

Multi-round modes should distinguish score lifetime from combat callback lifetime.
Play Tag retains its frozen roster and score objects but advances the combat token
and participant combat epoch at round boundaries. The existing pending revival
queue survives those boundaries; ready/alive checks reserve the next round until
recovery finishes. Do not reuse the initial countdown's unconditional K/D/A reset
for later rounds. See [Play Tag rounds](play-tag.md#configurable-rounds-0610-2026-10-18).

Projectile audits must inspect native runtime homing activation as well as prefab
flags, and traverse fire-data subclasses rather than just their base type.
Mini Ballista enables homing conditionally; crossbow mines instead cache a faction
mask at spawn and need a live pair-policy check at proximity detection. Use exact
damage/prefab identities, including dash versus special attacks, and reuse stock
movement replication. See the [weapon/artifact audit](friendly-fire-projectile-audit.md).

Optional PvP limiters can share the existing directed receipt-pair balance window
without sharing their setting-revision reset. Hit-frequency scaling uses a
separate revision: resetting its rate estimate must not mint damage-budget
credit or reset the other effect clocks. Count admitted positive resolved hits
before budget consumption and native callbacks to cover nested procs and
companions consistently. See [frequency model and lifecycle](pvp-hit-frequency.md).

Native `CharacterDebuff.Update` runs the server tick before its expiry check,
even while an ended effect waits 0.5 seconds for network destruction. A PvP
duration cap must therefore guard before the native update and suppress late
ticks after termination; merely changing a timer length can still allow a final
overdue hit. Burn inherits the base `RefreshDurationTimer`; Plasma overrides it
but calls base after its native charged attack. Both use general and Burn-specific
duration bonuses, and both implement Blue Flame/evolution within the same native
classes. Recognize both sibling families (`0.60.1`) rather than only Burn or
all fire-element damage. Renewing `AddStack` refreshes even at maximum stacks.
Track that refresh on the existing origin
receipt to cap each application at two seconds without changing native timers;
non-renewing stacks keep the previous deadline. Native `Destroy` retains removal
callbacks (including Plasma's expiry shock) and guest despawn. See
[Burn duration evidence and scope](pvp-balancing.md#burn-duration-cap-0592-2026-10-16).

Artifact audits must follow the instantiated secondary component, not only the
`Charm_*` trigger or `Bullet` prefab. Plitvice (1248, `Item_FrostMP_Name`) uses
`Charm_IceSword` → RPC-created `IceSword`; the latter had an inline player-mask
veto before central damage. Flame Sword combo auto-search had a separate veto.
Both now adapt to shared hostility without cached participation or new packets.
`EMBER` and `FLAMESWORD` are different categories; asset IDs and serialized
component references disambiguate displayed names. See the
[secondary sword audit](friendly-fire-artifacts.md#plitvice-and-secondary-swords-0591-2026-10-16).

Native companion punishment uses the same `NPCDeadCheckServerside` callback as
ordinary social NPCs. Its crime debuff affects the whole party, while saved
`CrimeCount` causes the buff to be reapplied during player initialization.
`UnitAI_NewBasic.OnDamaged` can also alter global faction relations before death.
Exempt an admitted friendly-fire companion at these exact boundaries; removing
the visible buff afterward misses persistent consequences. Capture ownership
before `Charm_SummonUnit.HandleUnitDie` clears it, and retain native cleanup.
See [audited symbols, scope and tests](friendly-fire.md#companion-penalty-exemption-0571-2026-10-16).

Role-dependent PvP modifiers (`0.57.0`) should query the shared match's current
receipt and role at the resolved-hit boundary. An avatar or netId alone cannot
prove that a reconnect still owns a bomb. Require the same run, phase, connection,
living avatar and armed/unexpired fuse. If incoming damage reduction follows a
token reservation, refund in the reservation's original units: divide actual
native consumption by the hit's captured reduction factor. Otherwise rapid hits
can refund away the intended reduction. Track role revisions to detect an
A-to-B-to-A transfer between attacks without resetting accumulated credit.
See [Play Tag chaser accounting and tests](play-tag.md#chaser-advantages-0570-2026-10-16).

Cosmetic RPC readiness is distinct from gameplay-state readiness. A positional
impact message on `DungeonManager` needs that anchor and a current ready
same-floor recipient; it does not consume the victim's observer membership or
the recipient's inventory/race/stat readiness. The kill-audio follow-up in
`0.54.3` removes those extra gates and adds bounded dispatch traces. Validate
resource registration and the native call all the way to FMOD `start`, not just
the outgoing payload. Even nonzero offline mixer output cannot prove live guest
audibility. See [remote audio evidence and outstanding reproduction](remote-event-audio.md#kill-audio-follow-up-0543-2026-10-15).

PvP balancing (`0.52.0`) uses the existing resolved-damage boundary after native
defense/critical/true damage and before regular/MP shields. Read victim `MaxHp`
at each permitted player hit; avoid rewriting native stats to normalize PvP.
This keeps live costume/menu/buff changes native and eliminates stat restoration
journals. Since `0.53.0`, a shared per-attacker/opponent token budget also limits
rapid hits. Keep its units relative to maximum HP, not current HP, and reserve
before callbacks so nested artifact procs share it. Native MP shields truncate
damage to integer MP: spending fractional tokens without reconciling actual
damage would make rapid low-value hits unable to drain MP. The three native
`AddReceivedDamage` calls report regular shield, MP shield and HP separately;
use their sum to refund unused reservations only after successful native damage
completion. Generation checks prevent old refunds replenishing reset windows.
`CharacterDebuff_Burn.OnUpdate_Server` advances its separate `tickTimer` using
`Time.deltaTime * (1 + BURNSPEED/100)`. The base `CharacterDebuff.Update` owns
expiry on another timer. Since `0.59.0`, Burn, Plasma Burn, Frostbite and Poison
use independent configured intervals, each defaulting to 0.75s for current PvP
player victims. Wrap the exact periodic `Timer.Update` call and advance by raw
delta times native timer length divided by configured seconds: preserve native
elapsed storage, reset-on-tick and activeOnce without writing timer length.
BURNSPEED is intentionally bypassed only under that PvP policy. Wound keeps the
earlier `0.54.2` 0.75 elapsed multiplier. Blue-burn damage, stacks, expiry and
Plasma's separate shock accumulator remain native. See [DoT intervals](pvp-dot-intervals.md).
It consults shared live hostility;
no prefab, stat, timer interval or cached override needs restoring. Native tick
damage still goes through the same resolved-damage budget (configurable since
`0.55.0`, default 70% max HP/s since `0.55.1`). Rate changes preserve tokens and pulse clocks;
the interval spanning an edit uses the lower of the old/new rate to avoid
retroactive higher-rate credit. The following interval uses the new rate.
Electric/Plasma shock and Freeze have application/refresh/expiry damage rather
than periodic ticks; separate 0.25s clocks (tuned in `0.54.4`) in that existing pair window limit
only their damage. Freeze discards ApplyDamage's result before calling native
StartStun(BuffDuration), so zeroing a repeated damage pulse preserves the stun.
Dark Cloud's existing pair cooldown is gated by the same preference. See
[model and limits](pvp-balancing.md).

Dark Cloud (`0.51.2`, cooldown tuned to 0.25 seconds in `0.55.2`):
`FireLightning` invokes `OnAttackUnit` after `ApplyDamage`
without checking the damage result; Blazing Stormcloud uses that callback for
burn. A player-only rate limit must guard the whole strike to suppress repeat
debuffs too. Native multishot coroutines can continue after owner death, so a dead
but current owner must retain the same limit. Caller cloud costs and discharge
notifications occur outside that method. See [strike boundary and lifecycle evidence](friendly-fire-artifacts.md#dark-cloud-player-strike-limit-0512-2026-10-13).

Companion stat controls (`0.51.0`) modify native owner `FOLLOWER*` bonuses.
Existing AIs/combat and three companion revival timers read the live leader;
do not mirror bonuses into each follower's dictionaries or cache a leader across
ownership changes. The six status definitions are an explicit user-requested
extension beyond C-menu rows. See [units and native evidence](companion-stat-modifiers.md).

Automatic respawn visuals (`0.50.0`): `Revive` restores the avatar but does not
spawn the white pillar. `RpcCreateAdditionalLifeFx` is a separate native
presentation RPC at the avatar's world transform, including a brief observer-wide
flash/sound. Emit only after actual recovery and current-scope revalidation;
successful no-op returns are insufficient. See [native evidence](respawn-visuals.md).

Private killed-by chat (`0.49.0`) reuses the native `DungeonManager.RpcChat`
receiver through Mirror targeted dispatch. A null avatar preserves existing
overhead countdowns; only the victim sees the matchup from their perspective.
Pair totals share KDA score identity but reset explicitly on run/scope changes;
life invalidation alone is also used between Tournament rounds and must preserve
history. See [private notice and lifecycle evidence](friendly-fire-killed-by.md).

Downed-player visuals (`0.48.0`): the native red single-player crash is a local
world-space SpriteFx, not screen-space UI. Stock casting RPCs can select it but
introduce transparency/material side effects. Use the separate native impact
broadcast for the friendly-fire indicator, with current participant receipts at
the shared death boundary. Never replay death/damage just to show an effect.
See [death visual findings and transport limits](friendly-fire-death-visuals.md).

Spell targeting (`0.47.1`) reuses the artifact hostility adapters but needs exact
Magic projectile identities. Native homing acquisition is host-controlled;
ordinary player-input aim is separate. A passing faction predicate is insufficient
when a physics query first fails to resolve the player's costume hitbox to its
avatar. See [Bolt/shared spell targeting](friendly-fire-spells.md).

Weapon projectile damage IDs can be generic: Liberation carries `DirectAttack`
and `Weapon_SpecialAttack`, shared with unrelated special attacks. Its four
exact `FuryMPBullet_*` prefab keys distinguish it. The native spawner assigns
`ProjectileBase.spawnedPrefabName = key.name` on every spawn; use that managed
field alongside damage identity, rather than Unity clone names or the owner's
currently equipped weapon. This reuses shared homing without caching stale
eligibility. See [Liberation resource and native contracts](friendly-fire-liberation.md).

Tournament (`0.47.0`) shares `CombatMatchRuntime` with Deathmatch but captures a
finite roster of connection receipts for its bracket. Do not key bracket progress
by account or net ID: reconnects are new lifetimes. Pair changes invalidate combat
and assist receipts while retaining shared KDA totals, and selected-connection
pruning cancels Tournament just like a selection command. See [Tournament](tournament.md).

The project retains gameplay effects usable by unmodified guests through native
state, existing items/prefabs and native RPC consumers. Local settings UI,
translation, diagnostics and update controls support those features; they do not
promise to alter guest UI definitions.

| Lifetime/boundary | Finding and consequence |
| --- | --- |
| Hosted session | Owns current intent. A fresh run in the same session can retain intent; a new session resolves a new scope and explicitly saved preset. |
| Run/save and dungeon | Restart can reuse avatars/dungeon while clearing native dictionaries. Track run generation/save identity; avatar presence alone cannot prove initialization is current. |
| Chapter progress versus settlement | `DungeonManager.victoryType` is an outcome marker, not an ended-run flag. Chapter events set 2/6 before play ends. Recovery retains `CurrentRun.enableSave`, leave-request and exact-scope guards; see the [2026-10-11 correction](deathmatch.md#late-run-availability-fix-0431-2026-10-11). |
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

Rabbit's Iron Wall and Blood of Obrus (added in `0.46.0`), and optional
Mole/Farmer Squirrel/Turtle Collin, are native costume-bound starting grants.
Preserve exact grant receipts through restart,
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
chain. Since `0.39.2`, six audited burn/debuff item selectors also read this policy;
do not globally change faction masks used by healing and unrelated skills.
`ApplyDebuff` has no faction veto, but upstream selectors and nested electric
damage needed separate treatment. Preserve immunity, proc chance and duration.

The `0.43.2` [artifact audit](friendly-fire-artifacts.md) adds another boundary:
offensive automatic effects can require `IsInBattle` before even searching for a
target. Never globally set that flag for PvP; player healing and travel also read
it. Scope the extra nearby-player check to audited offensive consumers. Likewise,
wrap only artifact calls into generic nearest-target utilities and identify
homing bullets by their current native identity, not a pooled-object cache.
Native attack callbacks can apply damage synchronously; explicit callsite
receipts and a bounded depth admit those procs without reopening recursive
artifact/reflection loops. New consumers need native IL and executable coverage.

Debuffs can outlive their caster, and companion death clears `NetworkLeader`.
Current-owner-only classification therefore loses scaling/off protection. Weak
per-debuff origin records carry that classification only through native effect
operations; lost caster/owner or owner transfer ends the old effect safely.
Capture kill names/companion ownership before death callbacks, and distinguish
unnamed NPCs from missing player names with typed localized fallbacks.
For KDA (`0.40.0`), use native `AddReceivedDamage` accounting instead of attempted
hit counts or post-callback HP comparisons: shield/MP-shield/HP damage is already
resolved, and the player's override calls the base method before death. Keep
scores separate from weak per-avatar-life contribution sets. A native extra life
does not call `Die`; `ForceDie` bypasses it, so actual `Revive` must clear old-life
contributions too. Use nonzero `PlayerSpawner.steamID` for session score identity,
never display names or reusable slots. A generation token prevents in-flight
deaths from undoing toggle/run resets. These counters are host-only chat metadata,
not new player SyncVars or saved preset fields.
For recovery (`0.41.0`), call `UnitAvatar.Revive(MaxHp)` on the server instead of
writing death/HP flags: it invokes native inventory, protection and RPC paths,
and `GameCamera.OnRevive` resets the spectator target. `PlayerSpawner`'s
`HandleDieServerside` emits game over on a party wipe. This must be prevented at
the exact friendly-fire death boundary if the host is to recover that party:
`UI_GameOverLabel.OnOpened` disables/deletes the current run save and game-over
events settle quests. Native revival cannot undo that process on stock guests.
The action rejects disabled run saves and requested leave/restart transitions,
not nonzero outcome markers from chapter progress, and does not attempt rollback.
Revival also clears `IsDead` before HP/revival callbacks and only then sends the
guest RPC. Catching an exception around the entire `Revive` call is insufficient:
retry would skip that alive-but-incompletely-restored player. The recovery action
uses exact-avatar event wrappers to continue later subscribers and native tail,
logs callback warnings, and preserves ordinary native invocation outside it.
See [combat and the native audit](friendly-fire.md).

Timed deathmatch recovery adds a second ordering constraint: stopping from inside
`UnitAvatar.Die` must wait for its finalizer, because native `RpcDie` follows
server death callbacks. Sending revival inside a callback would let a later death
RPC hide an already-alive guest. Also invalidate both active timers and stop
cleanup on any native revival; exact avatar identity alone cannot distinguish two
lives of the same avatar. Native `DungeonManager.Chat` with an avatar supplies
stock overhead bubble replacement even for dead avatars. See [deathmatch findings
and tests](deathmatch.md) for the shared implementation and limits.

One concrete disconnect risk was a recognized talent-budget rejection escaping a
Mirror command handler. Its targeted containment is distinct from initialization,
where unsafe saved allocations must still stop loading. Host logs also showed
peer closes, a timeout and queued-send-limit errors; these did not prove a single
addon or stat cause. Do not suppress arbitrary exceptions or weaken state guards
to hide disconnect symptoms. See [investigation](disconnect-investigation.md) and
[penalty/transport review](penalty-stat-sync-review.md).

Per-player combat selection (`0.45.0`) is connection-bound, unlike KDA's session
account identity. Mirror can leave `isReady` true in transport disconnect cleanup;
host disconnect saves run before old dictionary membership is removed. Check the
exact connection-map object, identity/spawn map and a retirement generation.
Removing the last selection enables everyone else, never the retiring player.
`PlayerSpawner.Initialize` resets session/avatar participation; ordinary costume
equipping does not. See [native lifecycle contracts](selective-friendly-fire.md).

Selection edits during a deathmatch revival must not cancel the admitted revival
tail: native `IsDead=false` precedes callbacks and `RpcRevive`. Validate the captured
connection/run until completion, while checking current selection only before
admitting a new respawn. Floor-load readiness delays execution without replacing
its original countdown. Excluded environmental deaths keep native game-over rules.

HP/MP Harvest is awarded inside `UnitAvatar.ApplyDamage`, after shields, actual
HP damage and attack callbacks but before death. `HPSteal` plus per-hit
`DamageInstance.hpSteal` feed `Heal(float)`; `MPSteal` feeds `HealMpFloat(float)`.
Both use rates divided by 1000 and undo positive `RECEIVEDDAMAGEINCREASE`.
Reduce exact recovery inputs rather than integer stats or all healing calls:
`HealMpFloat` retains fractions in `mpStack`, while unrelated potion/regen and
nested PvE recovery must remain native. The host's native HP/MP SyncVars serve
unmodified guests. See [20% balanced PvP Harvest](pvp-balancing.md#harvest-recovery-0572-2026-10-16).

Evasion has separate stat and guaranteed-item paths in `UnitAvatar.ApplyDamage`.
The stat path uses a logarithmic chance after a 10000-point input clamp, then
applies attacker `IGNOREEVASION`. Per-hit `absoluteEvasionPercent` and barrier
`ABSOLUTEEVASION` follow if the first roll fails. Preserve the latter's
`USE_ABSOLUTEEVASION_STAT` flag and `OnEvade` callback to consume the barrier.
Dash protection invokes `OnEvade` earlier too; a first-match callback lookup is
not sufficient to identify the stat/barrier boundary. For PvP-only adjustments,
transform the chance in the current hit rather than patching generic stat reads
or random-number generation. See [50% Evasion cap and native contracts](pvp-balancing.md#evasion-cap-0604-2026-10-17).

Guard cost has another distinct boundary in `UnitAvatar.ApplyDamage`: native
base 10f is reduced by `GuardResist`, halved by perfect guard, then processed by
`OnCalculateDurabilityDamage` after `OnGuardSucceeded`. Replacing only its final
integer conversion before `UseMp` preserves native guard break, infinite MP,
callbacks and MP replication; wrapping every `UseMp` would incorrectly affect MP
shields. Read current policy/maximum MP at that final boundary because callbacks
may change them. Float cancellation at 99% efficiency can turn an intended 1 MP
into 2 when ceiling a scaled amount; normalize exact native float formula results
before scaling, retaining unrelated callback values and positive tiny charges.
See [guard scaling and contracts](pvp-balancing.md#guard-mp-cost-0580-2026-10-16).

Waiting-room recovery has distinct native sources: `PlayerAvatar.Update` provides
the heal-place HP tick; `UnitAvatar.Update` drives passive HP/MP and peace-time MP
recovery. Scope any suppression to these exact calls, not all `Heal` operations
or the entire Update. Identify the actual player floor using generated
`FloorData.stageName == Race.lobbyStage.name`; safe-mode and run flags include
other situations. See [lobby recovery control](lobby-recovery.md) for contracts,
participant/match integration and verification limits.

Visual RPCs are not necessarily audiovisual: `RpcBloodFestivalHealFx` and the
red `PlayerDefaultBigBullet` destroy presentation are silent. The existing potion
drink-visual RPC broadcasts actual audio as well as green particles without
raising potion gameplay events. Player death audio is locally owned, so exclude
the victim when adding a stock-client cue for observers. Targeted native messages
also reach the host's local connection; extra direct host playback duplicates
them. See [event-audio evidence and send-failure handling](remote-event-audio.md).
