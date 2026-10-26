# Sephiria domain findings

Zombie potion admission (`0.97.0`) must use the current round faction, including
pending conversion, rather than the alive-only combat role helper. Recheck at
native drink completion to close animation/conversion races; preserve native
catch/unwield cleanup before all drink events and decrement. This is distinct
from PvP consumption and supply grants. Zombie movement now uses 70% of the
owned native base speed. See [Zombie Survival](zombie-survival.md).

Dark Cloud presentation (2026-10-24): extending native targeting does not extend
native FX delivery. Player strikes call `TargetCreateFx` on the cloud owner's
connection. Its stock positional lightning/ice/EX receiver is cosmetic and does
not require ownership, so 0.94.5 forwards admitted PvP strikes to current, ready,
same-floor observers without guest addon code. Preserve native variant arguments
and the owner send, snapshot observers, and recheck connection/identity lifetimes
before each additional send. Cosmetic recipients do not need inventory readiness.
The separate range ring is locally gated by `isOwned`. See
[visual implementation and evidence](dark-cloud-pvp-visuals.md); live rendering
and audio remain unverified.

Rabbit/PvP potion precedence (`0.96.2`): active participants use one authoritative
HP consumption rule, selected by `PvpPotionPolicy.RuleFor`. General PvP infinite
off always consumes; costume retention is available only outside active PvP.
The former optional Rabbit override is retired, including legacy preset opt-outs.
A finite/PvP-admitted drink cannot become costume-infinite if a callback ends PvP.
Keep native completion/death/Survival guards and independently configured
entry/respawn supplies. No refunds or alternate inventory writes. See
[potion policy](pvp-potions.md#authoritative-pvp-consumption-0962-2026-10-27).

Zombie movement (`0.93.1`): `UnitAvatar.NetworkmoveSpeed` synchronizes the base
used by both walk/run consumers, separately from additive equipment/status,
frostbite and weapon speed modifiers. A role-owned ×0.8 base adjustment reaches
stock guests, whereas a host-only movement getter patch would miss client-owned
movement. Keep exact original/applied values on the captured roster and restore
only owned values; unchanged match ticks need no writes. See [movement evidence](zombie-survival.md#zombie-movement-penalty-0931).

PvP elemental limits (`0.93.0`, 2026-10-24): Physical/Flame/Ice/Lightning inputs
are baked into source damage before the victim is known. The approved fallback
caps active participants' effective `GetConvertedElementalDamage` result, also
affecting their simultaneous PvE attacks. Preserve native highest-element,
potion, stat-derivation, unlock/script and C-menu reads to avoid permanent
side effects. No raw stat writes or restoration ledger; existing attacks retain
native creation-time snapshots. See [elemental scope and evidence](pvp-elemental-limits.md).

Ice Weapon targeting (2026-10-24, `0.92.0`): audit activation, prefab homing flags
and movement consumers separately. Frozen Bow auto-acquires; Hammer's scythe
follows synchronized owner aim; Plitvice deliberately orbits. A stationary
owner-following artifact body does not prove a failed projectile search. Emission
admission can also suppress launch without changing that body. See the
[family audit and remaining live uncertainty](ice-weapon-targeting-audit.md).

PvP periodic recovery (`0.92.0`): rolling cadence history and stored damage
capacity serve different purposes. Recover both through one directed-pair clock,
not separate weapon timers. Reuse history arrays, keep effect cooldowns intact,
and use a separate recovery generation so an outer native callback cannot refund
into a recovered reserve while its unused assistance can still be returned.
Fractional missed deadlines must advance past now and reject duplicate timestamps. See
[semantics and verification](pvp-hit-frequency.md#periodic-recovery-0920-2026-10-24).

Secondary emissions (`0.91.0`): active count and creation throughput are separate
budgets. Rate credit must be reserved before callbacks and never refunded merely
because an object despawned. Host RPC loopback is queued, so a sender-stack
reservation cannot track a later local instance. Raw network deletion also skips
native teardown; generic FIFO is not a safe alternative to admission gates.
See [new coverage, rate controls and FIFO evidence](projectile-emission-controls.md).

Host-only presentation (2026-10-23): a native spawn envelope carries root scale,
but the shipped player transform has scale synchronization disabled. Initial
scale and live resizing are different capabilities; changing serialization flags
only on the host is incompatible. Native FX messages can select stock assets,
while host asset/material edits cannot distribute new appearance. Weather has a
retained SyncVar but renders only on supported floors. See the
[capability matrix, exact contracts and candidate features](host-only-capabilities.md).

PvP lower-bound assistance (`0.90.0`): post-percentage hit damage relative to
victim maximum HP is a practical runtime measure of offensive pressure across
different native formulas. A shared bonus pool prevents each tiny packet or
Planet source from receiving a fresh percentage floor. Apply frequency to both
base and bonus; reserve/refund only funded credit before callbacks, then retain
role and shield reductions. See [model and limits](pvp-lower-bound.md).

PvP burst (`0.88.0`): a nonlinear per-packet curve rewards splitting damage even
with hit-frequency scaling disabled. Use one attacker/opponent recoverable
reserve for heavy hits and barrages; stored capacity and sustained refill are
different controls. Native shields/armor and role factors remain after the
reserve, so refund only unused funded credit. See [damage-path findings](pvp-burst-audit.md).

Lobby dice (`0.86.1`): initialization is not the only time starting-dice intent
must apply. Pre-run live changes reconcile maximum and balance by the allowance
difference, checkpoint ownership and preserve spending. Observe maximum/ownership,
not spendable balance; gate by both live and saved run-start flags to exclude later
town visits. See [lobby dice findings](lobby-dice.md).

Autonomous artifact cadence (`0.86.0`): serialized damage IDs distinguish seven
automatic emitters from weapons, books, planets and armaments. Fire/ice Guillotine
share one ID; `FROST` (Frost Relic / 얼음 무구) differs from `GLACIER` (빙하).
A zero resolved damage result still runs native callbacks, so early absolute
rejection plus late shared-ledger reservation is required. Chakram/Echo statuses
are independent of direct-hit return values. See [audit and boundaries](pvp-autonomous-artifacts.md).

Burn-family cap (`0.85.0`): the existing effect-origin receipt stores the last
native application/refresh time. Live duration configuration must read that age,
not reset clocks on a settings edit or mutate native timers. Compare the float
clock to `appliedAt + duration` to avoid subtraction keeping a decimal boundary
alive. Shared native Burn/Plasma types cover blue/evolved forms; other effects'
tick controls do not imply duration caps. See [duration semantics](pvp-dot-intervals.md#configurable-duration-cap-0850-2026-10-22).

Perfect Guard (`0.84.0`): native `UnitAvatar.ApplyDamage` computes 10 MP →
GuardResist → conditional Perfect Guard x0.5 → OnCalculateDurabilityDamage →
integer truncation → UseMp. Record the actual half-cost branch, not a later
timer read or a guessed cost (artifacts can modify both). Validate the same cost
local across the branch and final spend. The current-hit scope safely carries
classification across callbacks/nesting. The optional native-cost multiplier
is explicitly exempt from the general shield floor. See
[semantics and evidence](pvp-shield-mp.md#perfect-guard-multiplier-0840).

Additional defensive effects (`0.81.0`): Elastic Band restores dash charges on
every evade, including Battle Bracelet's dash-invulnerability evade event.
Limit that specific refund callback, never the shared evade event (which also
consumes defensive charges). Extended katana/dagger counter immunity uses a
separate signed-byte counter and a resettable weapon-controller timer; adapt
the PvP consumer without mutating the native PvE window. See the
[artifact/asset audit and bounded limits](pvp-defensive-effects.md).

Shield MP floors (`0.80.1`): Iron Wall is a `GuardResist` bonus, not an MP-shield
replacement. The shared guard, parry, ordinary shield-point, MP absorption and
`UnitSubShield` paths are distinct. Enforce once per hit, after native reductions,
using overflow-safe maximum MP; preserve native shield absorption and partial
HP spill. See [audit and extension boundaries](pvp-shield-mp.md).

All Damage Amplified (`0.80.0`): `UnitAvatar.ApplyDamage` sums attacker and
optional leader `AllDamageBonus`, then adds `damage.damage * bonus/100` alongside
other bonuses. Limit this one combined float contribution for balanced PvP;
globally patching the stat reader would affect PvE and double-limit companion
bonuses. C-menu `FINAL_DAMAGE` is the bonus plus native 100%. See
[amplification boundaries and curve](pvp-amplification.md).

Secondary instance accounting (0.79.0): the native `ProjectileSpawner<T>.Pool`
name does not imply reuse; it instantiates and network-spawns fresh objects.
Guard audited emitter call sites, because Pallas/FuryMP/Yearning dereference
spawn results. Release at actual despawn/destruction, not `DestroySelf`, which
can begin a delayed explosion. Preserve child provenance before teardown.
See [secondary projectile limits](projectile-limits.md) for coverage, contracts,
performance design and stock-guest limitations.

The `0.90.3` [effect performance audit](projectile-performance-audit.md) confirms
that all six damaging FROST/Ice Weapons sources are outside that registry,
including converted fire variants and separate Eternal Eclipse Flame Sword
callbacks. Ice Vine is not the FROST family. Destruction-module children of
tracked missiles inherit counters, but local/RPC attacks, ground records and
client visual trails do not. Instance caps and creation-rate budgets solve
different problems; do not infer rendering or bandwidth cost from live count.

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

Ratio-based combat policy belongs to the match, not individual avatars. Zombie
Dynamic Balancing counts committed teams including pending respawns; using
currently living players would let death manipulate the ratio. Settle the old
smoothing target at conversion before changing it. Continuous value changes must
not repeatedly bump the shared role revision: that revision protects elapsed
damage-budget credit across discrete role changes, not animation frames.
The [bounded Zombie curve](zombie-survival.md#dynamic-balancing-0760) uses existing
role/damage consumers without new network state or native stat writes.
The `0.78.0` minority curve crosses neutral at equal teams, raises majority-Zombie
incoming damage above 100% and protects minority Humans through that same
incoming-role boundary. A single live-team lookup must reject pending/dead and
stale/spectator roles for either side. Cached curve outputs avoid repeated
polynomial work at a shared timestamp. Keep the shared damage ledger in pre-role
units even for vulnerability above 100%; otherwise refunds can manufacture credit.

Native overhead chat cleanup requires a nonempty replacement: both
`DungeonManager.Chat` and its stock `RpcChat` receiver reject whitespace before
`CreateChatBubble`. Empty-string sends do **not** remove a remote bubble.
`UI_ChatBubble` follows its player and expires after three seconds; refreshes
replace it. The [Tournament status lifecycle](tournament.md#live-opponent-status-0740-2026-10-20)
uses terminal notices plus exact connection receipts, without overwriting the
subsequent respawn bubble or sending into another run. Native chat also writes
the persistent log; it is not an overhead-only transport.

Starting-item registration and delivery are separate lifetimes. Native
`AddStartingItem` registers before delivery and suppresses delivery inside a run;
`RestockStartingItem` delivers registrations on fresh runs, not each floor. Ordinary
live gifts therefore use a separate native inventory path. Requital (보은) is
ID **2100**, an Eternal Slate whose active type is **WitchHat**, not Default.
Heaven (천국) is ID **2099**, Eternal StoneTablet, active type **Hidden**,
name key `Item_StoneTablet_Heaven_Name`. Native direct inventory insertion does
not filter this catalog flag. Both native assets can use the same grant service.
Saved exact-ID provenance must include the entity type: absent type markers
represent legacy Requital, not whichever type the host currently selects.
Do not select all Eternal items or reject this known costume-type item solely
because of that active type. Automatic ownership must use exact instance IDs,
not item type; original costume/passive Slates and manual gifts remain separate.
See [Requital grant and retraction evidence](requital-slates.md).

Debuff names and elemental damage types are not interchangeable timing domains.
Burn/Plasma/Frostbite/Poison/Wound have periodic timers; Electric and Plasma's
shock share the `DEBUFF_ELECTRIC` marker on refresh/expiry (the Plasma prefab
overrides both burn/shock IDs to `Debuff_Plasma`); Freeze adds stun after
its damage result is discarded; Chim accumulates charge then hits on expiry.
Dark Cloud must gate its full strike before attached burn callbacks. Preserve
these boundaries when adding controls: use shared policy, native timer progress
or existing owner/victim pulse windows, never a global elemental filter that
also slows direct magic. See [the complete audit](pvp-dot-intervals.md).

Native debuff reapplication refreshes `buffTimer` (lifetime), not the five
periodic damage timers. Electric/Plasma can deal shock damage during refresh;
Freeze's application damage is followed by stun independently of its result.
`0.69.0` therefore extends existing owner/victim damage clocks instead of
intercepting stack/lifetime callbacks. Eight default-on schedule toggles share
connection/life/floor/epoch cleanup and live settings. Gate damage before nested
callbacks, reserve only budget-admitted hits, and keep history while a toggle is
off. Do not restore Plasma's `addDamageTimer`: it is a native charge accumulator
whose reset follows a shock attempt, not the periodic Burn timer. Installed-game
contract tests assert that native stack/refresh methods do not touch periodic
timers; a changed game build requires rechecking that assumption.

Damage floors must be reserved in the same units as the balancing ledger, before
nested native callbacks. Friendly-fire budgets store pre-role fractions of victim
maximum HP; a final one-point minimum reserves `1 / incomingScale`, carrying any
fractional deficit until refill. Applying only a `max(1, damage)` after budget
admission would otherwise grant rapid attacks free damage. Exclude numerical dust
from refunds/depletion, preserve genuine zero admission and never floor again
after shield absorption. See [minimum-damage evidence](pvp-balancing.md#minimum-positive-damage-0671-2026-10-20).

Feature-specific opt-outs belong to the feature's policy, not the shared combat
participant service. Rabbit's `BlockPvpShare` (default true even for a zeroed
settings struct) gates only its shared healing checks; White Bread and other
participant rules remain independent. Re-read feature policy at the callback
boundaries, and version explicit opt-outs so older saves preserve existing
behavior. A default-on restriction also needs `on` as its recovery action; the
shared checkbox accepts a recovery value while existing toggles still recover
to off. See [Rabbit PvP toggle](rabbit-potions.md#pvp-sharing-restriction-toggle-0670-2026-10-19).

Some artifact effects are contributions to native fields rather than separately
applied buffs. White Bread adds to `UnitAvatar.invincibleTime`. A read-time
admission adapter can exclude just that contribution using the native unique-item
registry and shared participation policy, preserving original timers and avoiding
restoration bookkeeping. Preserve fall grace and separate invulnerability paths;
read serialized level values instead of assuming C# initializer defaults. See
[White Bread evidence and tests](pvp-white-bread.md).

A sustained stock-client visual can use a separately owned registered network
prefab only after auditing the entire client/server boundary. Disabling a host
NetworkBehaviour does not suppress Mirror's start callbacks or serialize its
disabled state to guests. The Zombie marker separately guards owned server
startup, disables automatic host Update, and invokes only native client visual
updates. Destroy the network object to run guest visual cleanup; do not apply
the real debuff or share native casting handles. See [Zombie role visual](zombie-role-visual.md)
for asset identities, observer behavior, transition recovery and limitations.

Team-changing modes can reuse the existing match pair policy without changing
native factions or character stats. Infection has separate committed/pending and
combat-active states: an infected player leaves their old team's targeting policy
immediately but gains the new role's combat bonuses only after successful native
revival. Remove their former assist contributions against new teammates without
resetting shared scores or other pending death notices. A last infection commits
the outcome before the deadline, but conclude/recover on the next tick: native
reflection may finish an inner death before the enclosing lethal attack calls
its own victim's `Die`. All affected avatars must still belong to recovery.
`CombatRoleSettings`/`CombatMatchRoles` now share Tag and Zombie impact-time
modifiers and conservative budget revisions. See [Zombie Survival](zombie-survival.md).

Cutscene progression authority does not imply presentation-clock authority.
`NetworkCutScenePlayer` broadcasts graph/node identity and termination, while
guests run local coroutines; an earlier end stops their presentation rather than
speeding it up. Bespoke events also contain client-local fixed waits, and no
general native rate transport was found. Prefer scene-specific native skip/
shorter-wait investigation over a host `Time.timeScale` change. See
[cutscene timing, native contracts and limitations](cutscene-speed-investigation.md).

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

Chat presentation audit (2026-10-18): the stock `RpcChat` receiver always writes
the HUD log before creating a player bubble. Private targeting changes recipients,
not presentation. Direct bubble creation is local-only; no bubble-only flag exists
in that packet. NPC speech can omit logs but is bound to AI components; native
floating text is a visually different alternative. Version `0.63.0` adds a native
floating countdown prototype alongside existing chat/bubbles, sharing payload
validation with Rabbit's private low-MP alert. Cosmetic failure does not disable
chat or gameplay. See [transport evidence and live-test limits](overhead-message-transport.md).

Floating-text emphasis (`0.90.2`): the stock `DamageParticle` prefab enables TMP
rich text, but its `PIXEL_BOLD` font uses a bitmap shader. A bold tag alone may not
increase stroke weight; respawn notices therefore also use 125% size. Sanitize
the message first, then append only trusted balanced formatting tags. Keep the
normal native prefab because the alternate size argument forces red. This changes
one existing cosmetic payload, not shared font assets or observer dispatch. Chat
remains until the revised presentation is verified. See [asset findings](overhead-message-transport.md#respawn-countdown-emphasis-0902-2026-10-23).

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
`from 2` excludes all of them. Merchant base-HP factors by stage are 1/2/4/6/7/8
(since 0.96.3, stage + 2 thereafter; ×1 for unknown progress), before native stage/co-op bonuses applied
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

Native transport/lifetime audit (`0.90.1`): `NextCommon.SendSocket` appends one
channel byte and returns `EResult`; `NextServer.Send` closes on `NoConnection` or
`InvalidParam`, but only logs `LimitExceeded` without retry. Mirror's
`OnTransportDisconnected(int)` runs `Cleanup` and `RemoveConnection` before
`OnDisconnectedEvent`; the game-manager disconnect callback is too late for live
projectile ledger counts. Capture those at transport close entry, read-only.
Native FrostTouch calls its base HUD update before dereferencing
`NetworkTarget.transform` for an FX RPC; a destroyed target can survive into that
derived tail. The optional guard preserves base/caller expiry and gates only the
derived FX work. These errors followed, rather than preceded, the observed drop.
Evidence, limits and verification: [disconnect/projectile audit](disconnect-projectile-audit.md).

PvP cadence classification (`0.77.0`) uses existing source/context and native
metadata, with two fixed histories per directed pair and one shared allowance.
Native `DamageInstance` defaults to `Magic`; `EDamageFromType.None` identifies
many incidental attacks. Growth daggers use `Charm_DaggerGrowth`; the
`FlameSwordReturn.Update` damage constructor explicitly passes `fromType=None`.
The `Charm_` prefix and synchronous artifact scope cover additional procs.
Recheck pooled metadata per hit; never cache classification on a damage object.
REA reconfirmed the installed hash above and these native contracts
(`ev_b381dbcc75732c4428252012b960402be7bb6c8a4cbdd7ce18a747c1bdcf087a`,
`ev_3283eaf5aa29e68d33bdd60982580822c91ca3d11644d220ad4549c9bb6fd08e`).

A positive damage floor at exhausted credit requires separating delivered
allowance from funded reservation. Refund only funded credit minus actual
delivery in matching pre-role units. Otherwise native armor/vetoes can refund
unfunded minimum damage into a new burst. The floor is intentionally outside
the strict DPS bound and still obeys native defense and debuff schedule gates.
See [current policy and historical fairness audit](pvp-hit-frequency.md).

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
Native `MaxMp` itself multiplies `FinalMP * maxMp` as signed int before converting
to float, and adds the bonus as int. Large valid inputs can therefore wrap.
Since `0.78.2`, balanced guard cost derives the same maximum with wide arithmetic,
retains ordinary native rounding, and saturates at the MP field's integer limit.
Do not treat the game's maximum-MP getter as overflow-safe or globally change
it when fixing one consumer; infinite-MP mode and passive MP shields are separate
contracts. Nine Star Aquamarines grant Final MP/Lake bonuses, not infinite MP.
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

## Native Large Potion supplies (2026-10-20)

`0.94.1` extends supplies to successful native revival completion. Capture the
exact receipt, activation revision and match token before `UnitAvatar.Revive`,
then invalidate only that participant's shared supply cursor in the successful
finalizer. A per-receipt attempt ID also rejects older nested completions and
life changes during inventory callbacks. Defer inventory work until the ordinary reconciliation tick; this
reuses native duplicate checks after revival's own inventory processing and
avoids per-mode grant code or per-frame inventory scans. Entry and respawn grants
default on; explicit saved opt-outs remain valid. See [current supply semantics](pvp-potions.md#large-potion-supplies-updated-0941).

`0.77.1` ties PvP HP retention to the existing controller completion scope and
Survival callback guard. Native Survival runs before the regeneration heal;
temporarily invalid scope/policy can let it run before state becomes valid again.
Retire the captured PvP receipt whenever that exact callback is allowed through
due to revoked policy or invalid identity. Rechecking only at final retention
allows both stat gain and a free potion. Fee waiver happens before callbacks,
never as a later MP refund. No new native hook is required; native contract tests
verify the existing ordering against the installed assembly.

See [PvP supplies](pvp-potions.md) for audited item ID 1, resources.assets path
28856, three-store duplicate detection, borrowed native inventory permissions
and stock-client delivery. Optional one-shot rules must keep readiness inside
first delivery: a transient readiness gate on a shared coordinator can otherwise
replay an already-applied or faulted action. Match supplies use the full captured
roster, including waiting Tournament players, rather than active damage pairs.

## XP restriction (0.73.0, 2026-10-20)

`LevelController.LocalAddExp(int)` is the authoritative convergence point for
XP pickups, magic-fountain rewards, stock guest XP commands and recovered XP
after rejoin. `LocalInitialize(level, exp)` restores saved progression separately.
Use the grant boundary for permission rules; changing EXP multipliers or freezing
levels after the fact misses sources or leaves level-up side effects. The native
rejoin flow equips its costume before awarding preserved XP. See [XP restriction](experience.md)
for contracts, behavior and test limitations.
