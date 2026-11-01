# RaidRaid scaling and reward investigation

Research only, 2026-11-01. No addon implementation, build, deployment, game
execution or multiplayer experiment was performed for this audit. Decompiled
material stayed outside the repository. These are behavior observations and
design recommendations, not a port or a claim of live compatibility.

## Scope and evidence

The inspected installed RaidRaid DLL reports version 3.324.0. Its SHA-256 is
`5ea5bddbb560f637636ea605172b9b4504cceb13bef085129a36723aa5787542`, module
`RaidRaid_test.dll`, MVID `3ea7e45e-d664-4e66-afc4-fe241d5c319b`. Private ILSpy
reading used `%LOCALAPPDATA%/Temp/sephiria-raidraid-investigation/main`.
Installed beta native contracts use `Assembly-CSharp.dll` SHA-256
`383a0d5ea9218b797d6eacd1f67964a73606ba50572b7af2d10ac5357b621d01`, MVID
`ba32c2d4-ef71-4869-856b-49ec7aea12f4`, with private export
`%LOCALAPPDATA%/Temp/sephiria-beta110/game`. See
[the beta audit](beta-110-compatibility.md) for build 26324456 context.

REA evidence IDs, usable as exact same-session retained references:

| Label | Evidence and coverage |
| --- | --- |
| R | `ev_bfb087e01046a30156d2c3e2f1d3a40e3794623f4e0478713e7c434fd035c5bb`: selected RaidRaid managed members; 11 types, 167 methods, 896 calls; all selected type names matched. |
| RC | `ev_8180ce7b4c2cb967191d7407bd96746d476c7466464eb0e9047b6c2180f813ac`: complete selected call-edge page from R. |
| RM | `ev_37eaa0e33a17fc07959fe7d6fd16d870ef358c903e52c09619bb5413d600a08b`: complete selected method page from R, with CIL hashes and instruction anchors. |
| G | `ev_92d32a3049b685426e31ec9a7e11330d46acad79d95ddb645de26eccb1e211bc`: beta members for UnitAvatar, LevelController, DropItemOnDie, Exp, Money, CombatManager, UI_GameOverLabel, RandomEnemyPhaseSpawner, EnemySpawner, CommonEnemySpawner and FloorGenerator. |
| G0 | `ev_176c2a3ac6c0c8abff2de0f518316dbbb4f95a193dfa49ae496f6b0172af2888`: G calls 0–999, including CombatManager.AttackEvent → HealPercent/RpcBloodFestivalHealFx. |
| G1 | `ev_9e92aa47f78726e9304e612343e4e85245be95737c649c70b08d5eca9e0b7a57`: G calls 1000–1999, including Money.AddToInventory → AddMoney and AddExp → LocalAddExp → NetworkcurrentExp/NetworkcurrentLevel/LevelUpOnServer. |
| G2 | `ev_e85c830d30fbc1a89061fb9d11635b2032cde985772bf7fed508e78f4878a36d`: G calls 2000–2999, including Exp.OnStartServer and UI_GameOverLabel.CalculateRunEarnedSapphire. |
| G3 | `ev_1afaf0399f78abb419e07f24a331a3db718cac9808e33cb2112685df2ff7af31`: G calls 3000–3999, including AddCustomStatUnsafe → SyncDictionary writes. |
| G4 | `ev_b85735f0f4f21977192c6e58757944c9710408c027511a50f3c72e7bf40d627f`: G calls 4000–4999, including AddMoney → NetworkcurrentMoney. |
| G6 | `ev_64a54ab16748df81b94f4da8a9f6e48f2a0b8a3c8b8c1ed7342bc0a7b4040d54`: G calls 6000–6999, including hp/maxHp/currentMoney generated SyncVar setters. |

R and G have complete selected metadata/member coverage. Their loaded user-string
subsets report ECMA terminal-flag discrepancies (one in R, multiple in G), while
preserving readable UTF-16; this does not establish runtime string acceptance.
Tokens below are local to these exact digests/MVIDs. CIL calls and ILSpy C# are
static evidence, not observations of execution or proof of all dynamic callers.
Private helper/patch files named below were also read; they are not all in R's
selected-type inventory. Native source reads complement G without implying that
G selected every native dependency.

## Gates, classification and application lifetime

`ScalerPatcher.Active` means `Settings.Enabled && StoryCompleted`. Story completion
reads the local host player's `mainQuestProgress >= 11`, cached for one second.
Enemy application/count/reward compensation add a `NetworkServer.active` gate.
There is no corresponding master/story gate in `InstantPickup.On`; that feature
only needs its own toggle plus server authority. Sapphire has its own run-start
hard-mode gate.

Classification is native `EMonsterType`: Normal=0, Miniboss=1, Boss=2, Dummy=3.
`KindOf` rejects values outside 0–2. `ScalerRunner.Register` rejects PlayerAvatar,
but does not reject leaders, friendly NPCs, merchants or companions by faction or
provenance. A non-player actor reaching registration with an accepted monster
type can therefore be considered for scaling. This is broader than “hostile
enemies.” Random spawn counts use `MonsterSpawnData.monsterType`; HP/damage use
the actual avatar's type. Beta RandomEnemyPhaseSpawner does not assign the former
to the avatar in SpawnEnemy, unlike EnemySpawner/CommonEnemySpawner, so the two
classifications must not be assumed identical for arbitrary authored content.

Floor resolution is not a universal floor number. `RefreshStageList` walks
`DungeonManager.sortedStages`, skips lobby/`...`, merges consecutive equal
localized display names, and stops after six names. It caches for up to five
seconds when the stage count is unchanged. `FloorIndexAt` selects the first live
generator within ±250 world units on both axes; it does not check exact floor
bounds. BossSpawner supplies its parent's floor as a stronger hint. Unit floor
cache keys are Unity instance IDs. Workshop maps, duplicated display names,
overlapping floor placements and more than six stages are unsupported assumptions.

Registration comes from the monster-type SyncVar setter, BossSpawner.OnStartServer,
and a RandomEnemyPhaseSpawner spawned-list collector. The runner defers one frame
before application, so normal native initialization can finish. The random
collector runs at most every 0.25 seconds. Neither path is a general “all existing
enemies” scan.

Application is once per Unity instance ID. `_seen` is recorded when queued;
`_done` is recorded before kind/floor validation. Failure to resolve a floor has
no retry, and a second registration with a better boss-floor hint cannot replace
an already queued entry. The run-start callback clears logs, not these identity
sets or floor/count baselines. Sets clear wholesale past 8192 entries; floor and
spawn-data caches clear past 4096. This is bounded memory, but not a lifecycle
identity guarantee: clearing may re-admit live objects, while reused IDs may be
skipped. Toggling/resetting settings does not undo existing scaled units.

Recommendation: use explicit server spawn provenance and floor GUID/stage identity,
capture one effective policy at a defined run/floor/spawn boundary, and retain
owned per-instance receipts with native destruction/run/session cleanup. Pending
initialization needs bounded retries and a success-only applied marker. Keep
players, player-owned companions and unrelated friendly NPCs outside enemy scope.
The settings snapshot should expose requested versus effective values, phase of
application, availability/failure, and current run policy without performing writes
when read. SephiriaOne's existing SessionSettingsSnapshot is a suitable model.

## Per-floor HP, damage and count

Observed formulas and limits (R/RM/RC):

| Feature | RaidRaid implementation | Recommendation for a new host feature |
| --- | --- | --- |
| HP | Independent normal/miniboss/boss arrays. Setters round to 0.25, clamp 1–1,000,000; the ordinary slider advertises a smaller 100 maximum. ApplyTo multiplies the current `maxHp` backing value by H through NetworkmaxHp, then writes Networkhp to preserve `old hp / old MaxHp`. It only applies above 1.001. | Multiply an owned native post-initialization baseline once; validate finite/ranged inputs and retain actual H for dependent healing/reward rules. Do not assume all HP components are multiplicative: MaxHp may contain other native stat contributions. |
| Damage | One per-floor additive percentage A for all three types, 0–500 in steps of 10. It adds A to ECustomStat 17, which is still AllDamageBonus in beta. `RequestStandardDamage` is used for logging; attack is not overwritten. Existing native AllDamageBonus and leader contributions still participate in damage. | Name the setting “additional all-damage bonus” if retaining these semantics. If a true final-damage multiplier is desired, specify it separately and audit the hit boundary. Keep the enemy-owned contribution separate from player stats and PvP limits. |
| Count | Integer 1–100 for normal/miniboss. Boss GetCount always returns 1, and SetCount ignores boss. Each spawn-data object captures original count, then writes `max(1, original × C)`. Dummy entries are skipped; unknown enum values fall into normal in this helper. | Keep bosses outside ordinary count multiplication; clone/own each runtime phase or adapt count consumption without mutating shared authoring data. Preserve an intentional zero count. Validate the spawner family and classify from verified spawn metadata. |

`CountScaler.Scale` covers EnemySpawner/CommonEnemySpawner.SpawnCoroutine phases;
`ScaleAll` covers all RandomEnemyPhaseSpawner phases before spawning. It returns
early if both normal and miniboss multipliers are 1. Consequently changing both
back to 1, ResetAll, or switching scaling off does not restore previously mutated
phase objects. If another category remains above 1 the helper does revisit the
phase and can restore the category now set to 1. Clearing the original-count cache
and revisiting still-mutated data can make the scaled value the new baseline.

Stock-guest feasibility is strong for HP/damage/count: beta hp/maxHp are generated
SyncVars (G6), customStats is a registered SyncDictionary (G3), and native spawners
create stock network entities. Existing guests and newly observing guests can
receive native current state without a custom addon protocol. This establishes
transport paths, not correct live update timing, presentation or reconnect results.

There is no reverse operation for HP/damage/pattern changes and no hot reapply to
existing units. ResetAll resets tuning arrays, selected boss picks and one hazard
flag; it is not a reset of every feature and not a rollback of the live world.
New port semantics should state whether reset means future spawns only or owned
live restoration. Avoid silently mixing both.

## Spawn pacing and performance dependencies

RandomSpawnerTracker separately raises the active-unit limit. If B is the native
MultiplayerLimit entry (party index clamped to the array), E is the original
additional limit and C is `max(normal count, miniboss count)`, it writes
`additionalUnitCountLimit = (B + E) × C - B`. Floor index 5 uses normal count only.
In the current beta B is selected from `[5,5,6,7,7,8,9,10,11]`. Native PROLIFERATE
then adds its own further cap increase during SpawnEnemy. Normal count and active
cap therefore interact with native hard-mode population changes.

`SpawnPacing.Accelerate` wraps only RandomEnemyPhaseSpawner.SpawnEnemy. It reflects
WaitForSeconds.m_Seconds and replaces positive waits at most 0.15 seconds with
wait/Cnormal. It leaves long waits and null yields alone. Beta's usual 0.1-second
per-unit wait is a match; a sufficiently short appearance wait could also match.
It swallows MoveNext exceptions as coroutine completion. The wrapper is neither
a per-frame spawn budget nor a guaranteed C-times throughput increase: Unity's
frame scheduling still bounds short waits, while physics/initialization costs
and the raised live cap remain.

Recommendations: make count, active population and per-frame spawn work separate
limits. Preserve the game's hard-mode ordering, wave-clear bookkeeping and
iterator disposal/error behavior. Avoid speeding every small wait by a private
Unity field heuristic. Use an audited native emission boundary with a conservative
frame budget. Instantiation, AI/pathfinding, collision, enemy bullets, death
drops and network observers all increase with population; no benchmark was run.

The collector caches reflected fields and tracks up to 64 active spawners. Timer
reflection is cached by AI type. These reduce repeated discovery work, but the
runner's pending loop has no work quota, count helpers allocate summary arrays,
and logs retain up to 5000 strings per category with RemoveAt(0) when full.
Those implementation details should not be copied as a performance design.

## Pattern speed

`Settings.PatternTimeFactor` is `1 / speed`, with speed 1–5 in steps of 0.1.
`ApplyTo` calls PatternSpeed only for minibosses and bosses, once at scaling time.
It examines MonoBehaviours on the unit root whose type names start with UnitAI,
selecting fields of exactly Timer whose lowercase names contain attack, pattern,
skill, cast, throw, shoot, charge, summon, spawn, fire, cannon, magic, laser, rush,
jump, breath or dash. It excludes bloodyrainpatterntimer, darknesspatterntimer,
crystalpatterntimer, tentaclesummontrytimer and obelisksummonertrytimer. A selected
Timer.time above 0.05 is multiplied by the factor; elapsed time is not rescaled.

This is selected cooldown shortening, not global animation/projectile/movement
speed. It misses timer arrays, float cooldowns, coroutines, child AI components,
private inherited fields and names outside the heuristic. In the beta QQ AI,
patternTimer is eligible while mouse-orb and diamond-explosion interval fields
do not match; the named crystal/tentacle exclusions remain explicit. Native Timer
still has public `time`, so that particular field contract survives the beta.
Changing settings later does not restore/rescale existing timers.

A host can drive faster native server AI and stock guests can receive its native
actions. A host-only local timer write is not itself replicated; every selected
timer needs an authority/consumer audit before claiming equivalent guest behavior.
Recommend a small explicit table of supported boss/miniboss timer fields and
server-only consumers, each with a baseline and compatibility check. Leave unknown
AI unchanged with a visible unsupported status. Enemy bullets are outside
SephiriaOne's existing player projectile source limits, so that feature does not
bound the extra load from fast enemy patterns.

## Festival of Blood compensation

`AttackEvent_Patch` sets one static InAttackEvent boolean around
CombatManager.AttackEvent, clearing it in a Harmony finalizer. The one-argument
UnitAvatar.HealPercent prefix excludes players, requires server authority, and
uses the unit's current floor/kind. With compensation q = percent/100 and current
configured HP multiplier H, the incoming heal percentage is multiplied by
`1 - q + q/H`. q ranges 0–1 in 0.05 steps, default 0.5. At q=0 the scaled-HP heal
is unchanged; q=1 offsets exactly H when HP is purely H-times native; q=0.5 gives
half of each contribution. This is not a flat reduction by q.

The current beta AttackEvent still reads BLOODFESTIVAL and calls the intercepted
HealPercent overload (G0). It excludes player attackers, elemental-effect damage,
dead attackers and fake-dead attackers; the victim must be a player. Native party
factors are 1/0.66/0.5/0.33/0.25 for 1/2/3/4/5+ players, with different native
constants for ordinary versus boss/miniboss attackers. Native heal penalties and
overheal caps still run afterward. The stock Blood Festival FX is unchanged.

Important gaps: compensation uses current requested H, not the H actually applied
to that unit; an unscaled/missed unit or mid-run change can produce the wrong
compensation. The static boolean can encompass unrelated synchronous heal calls
inside AttackEvent subscribers and is not nesting-safe. Recommend a narrow
Festival-only calculation adapter with invocation depth/context and an actual
applied-HP receipt. Do not touch general healing or PvP player sustain. This
remains a separate control when Sapphire difficulty settings are locked.

## XP/leaf compensation and instant distribution

`DropExp_Patch` and `DropMoney_Patch` cover normal/minibosses only, require active
scaling/server authority and a resolved floor, and use:

`f = max(1 / max(1, configured count), ExpLeafPercent / 100)`.

The setting is 0–1000 in steps of 5, default 0. Exactly 100 bypasses compensation;
Sapphire forces its effective getter to 0. At count 4, settings 0 and 25 both
produce f=0.25, 50 produces f=0.5, and 100 produces f=1. This is a lower bound,
not interpolation between native and scaled totals. Boss drops are excluded.

Each feature has a separate static double fractional carry, shared across all
units/floors/runs. XP becomes integer-truncated `exp × f + carry`. Leaves scale
only the unit's currentMoney; if that rounds to zero, randomDropMoneyPercent is
also zeroed. Otherwise the independent random `dropMoney` bonus is not scaled.
Consequently “0% keeps original leaf totals” is only an approximation, especially
when the random bonus dominates. Rewards use the count setting at death, not
proof that this enemy came from an actually multiplied spawn. The carry has no
run/session reset in these classes.

Recommendation: decide the exact desired reward policy first, snapshot actual
spawn provenance/count, account for the complete native reward including random
money, and scope fractional carry to a run and explicit reward category. Never
modify drop component fields before verifying this is an admitted real death and
the replacement operation can complete. Keep compensation separate from delivery.

InstantPickup uses lower-priority (200) prefixes after the normal-priority
compensation prefixes. Its XP branch suppresses DropEXP; its leaf branch zeroes
the two native money sources but leaves DropItems running for items/EXP/remote
inventory. The installed implementation has these exact economics:

- Leaves reproduce the seeded first RNG test and native Money/2 plus a 50% chance
  of ceil(dropMoney × randomDropMoneyPercent/100), breaking the resulting amount
  into 1000/100/10 drop buckets. Prefab containedMoney values are read once, with
  fallback 100/10/1. Party multiplier is 10,8,7,5,4 for 1,2,3,4,5+ non-null
  spawners. Per-bucket positive MoneyDrop bonus is truncated before multiplication
  by bucket count. Every non-null PlayerAvatar receives AddMoney; there is no
  alive, same-floor, readiness or distance filter. Followers do not drop money.
- XP reflects `isEXPAlreadyDropped`, marks it true before grants, and visits
  NetworkServer.connections. For each identity with UnitAvatar it applies that
  recipient's EXPDrop, divides each at-most-100 chunk by connection count clamped
  to 1–3 using Mathf.RoundToInt, sums the chunks, and calls LevelController.AddExp.
  Rounding per chunk is material: dividing the aggregate once can differ.
- The XP hook has no enemy-kind check and covers explicit calls to DropItemOnDie
  DropEXP as well as kills. It does not intercept every Exp entity in the game;
  chests or rewards that create their own Exp objects remain native.

These arithmetic/transport paths still match beta DropItemOnDie, Money and Exp.
XP AddExp reaches the server LocalAddExp and native currentExp/currentLevel
SyncVars plus level-up rewards (G1); AddMoney reaches currentMoney SyncVar (G4/G6).
Stock guests can therefore receive real native state without addon messages.
Direct distribution omits pickup motion/sound and awards earlier, including to
players outside pickup range; “same amounts” does not mean identical timing or
eligibility. Disconnected players are not credited for later kills. Beta preserves
unacquired Exp on disconnect and restores it through AddExp; direct grants create
no such outstanding pickup objects and do not implement a separate rejoin ledger.

Failure semantics need redesign: XP marks dropped before distributing and returns
false even if no eligible player exists; missing reflection means no reliable
duplicate guard. Money zeroes source fields before locating the recipient list.
An exception or disconnect during iteration can leave partial grants. The loop
has no bounded work cap (XP iterates every 100-point chunk), and large scaled
inputs use int arithmetic. Prefer arithmetic-equivalent constant-time chunk
calculation, checked ranges, a stable recipient snapshot with live connection
revalidation, and a single idempotent reward transaction. Keep the original path
when validation fails before any commit; do not replay after partial commit.

SephiriaOne already hooks LocalAddExp as the authoritative XP-block boundary.
Instant grants should continue to call AddExp, so general XP blocking and the
Rabbit exemption still work. Its expdrop/leafdrop character-stat settings naturally
feed the native bonus reads; do not apply those modifiers a second time. Native
level-up hooks, Rabbit grants and any inventory-per-level rule will run immediately
and may add work during mass kills. Reducing pickup entities can save object and
network work, but immediate level-up work and player-loop cost remain unmeasured.

## Sapphire progression and bonus

This is a reward rule tied to a difficulty run, not just a Sapphire amount toggle.
Turning it on forces the scaling master on and inventory-level interval to 5,
and changes effective minima. For zero-based floor i, HP minima are normal
`1+0.25i`, miniboss `1.5+0.25i`, boss `1.75+0.25i`; pattern minima are 1.5 on F1
and 2 on F2–F6. EXP/leaf compensation is forced to 0. The UI locks difficulty
edits during runs via BossExtraLock; the setters themselves are not a run lock.

At a run transition sampled every two seconds, SapphireBonus captures the toggle,
the initial CurrentBonus, HP-steal/augment/inventory/choices/earring flags, and
checks at least 50 hard-mode points plus positive BOSSPATTERN, MIBO and
BLOODFESTIVAL in hardModeEnvironment or hardModeShardLevels. Failure turns the
saved Sapphire option off. It samples the current bonus every two seconds while
running and keeps the minimum; it is not an event-complete historical minimum.
UI locks help ordinary interaction but do not make the runtime policy immutable.
The Sapphire gate does not itself check ScalerPatcher.StoryCompleted, and its score
uses Settings.Enabled rather than ScalerPatcher.Active. A UI unlock is therefore
not proof that every rewarded difficulty setting actually affected that run.

Exact `Breakdown` formula (RM token `0x060004b5`): let averages below use current
effective settings, `clamp01` clamp to [0,1], and floors mean the six configured
slots, whether actually visited or spawned.

| Contribution | Formula |
| --- | --- |
| Miniboss count | `275 × clamp01((mean(min(Cmini,6)) - 1) / 5)` over F1–F5 |
| Pattern speed | `180 × clamp01((mean(speed) - 1) / 4)` over F1–F6 |
| Attack | `157 × clamp01(mean(min(A,200)) / 200)` over F1–F6 |
| HP | `1188 × clamp01((mean(min(H,10)) - 1) / 99)` over 17 entries: normal/boss on six floors plus miniboss on five |
| Extra patterns | +200 if BossExtraOn |

CurrentBonus caps the sum at 2000. The HP numerator is capped using H=10 but its
denominator remains 99. Thus HP contributes at most 108, and the whole observed
formula tops out at 920 before perks; the advertised +2000 cannot be reached by
these settings. This is an algebraic consequence of the shipped formula, not a
runtime measurement. Normal enemy count has no reward contribution. Extra-pattern
bonus is initialized before the master-enabled early return, so that helper can
still report +200 with the master disabled (ordinary Sapphire enabling forces
the master on).

On grant it rounds the captured minimum, subtracts 50 for HP steal, 50 for
augments, 100 for inventory growth, 150 for choices, and adds 150 for an earring
ban. A nonpositive result is not granted. The +150 addition is after the 2000
clamp. It calls AddCustomStat("SAPPHIREBONUS", amount) and records RAIDRAIDSB in
the same player's customStats. This percentage adds to other native Sapphire
bonuses. Native beta settlement is `(level - 1 + sapphireInRun)` plus the ceiling
of that amount times effective SAPPHIREBONUS/100 (G2). Actual save/settlement timing
is client UI driven and was not tested with a remote stock guest.

Triggers are a tracked Unit_QQBoss or Unit_QQQBoss IsDead, or the
UnitAI_BossBasic.DieTruly postfix for UnitAI_QQQBoss. The latter is the clearest
final-boss boundary. The former accepts death of either tracked type without a
floor/completion/roster check; it is not by itself proof of final-clear semantics.
QQQ registration uses OnStartClient with NetworkServer.active, which assumes a
listen host. The beta QQ dramatic sequence proceeds into QQQ, so fake-death,
forced-death, phase changes and alternate routes need explicit testing before
using either type's generic IsDead as the reward condition.

`_granted` is set before distribution. Current non-null players are granted once;
there is no retry for players absent/unready at that moment, no participation
requirement, and no explicit reconnect catch-up. Only a later detected run start
removes the recorded old contribution. Disconnect/server stop resets local
tracking but does not run that removal. Native initialization clears/rebuilds
stats, and PlayerSpawner persistence saves orphaned status instances rather than
this ad hoc raw-stat marker; persisted/rejoined ownership must not be assumed.
The run-start minimum/perk snapshots are also not serialized.

Recommendation: postpone Sapphire until scaling/reward/extra-pattern coverage is
defined. Make an immutable eligible-run record tied to the native run identity,
actual supported difficulty policy and final-clear event. Define late join,
disconnect/rejoin, failed-run and settlement behavior explicitly, then validate
native stock-client settlement order. Use a separate owned contribution receipt
and authoritative one-time completion ledger. Choose a new documented reward
curve deliberately; do not silently reproduce the 920-versus-2000 discrepancy or
score settings that were not actually applied. Do not infer the user wants every
RaidRaid prerequisite, perk penalty, progression lock or custom reward rule.

## Interactions and implementation sequence

Current SephiriaOne has player stat ownership, XP restrictions, PvP balance and
player projectile admission; it does not already contain these RaidRaid floor
scaling/Sapphire systems. Its CharacterStats operates on PlayerAvatar snapshots
and uses SEPHIRIAONE_STAT markers, while RaidRaid scaling changes non-player
avatars. Keep those domains distinct. FriendlyFireAmplification limits the current
admitted player-versus-player hit, so ordinary enemy damage should remain outside
that cap. A mistakenly included player-owned enemy/companion can inherit leader
stats and enter companion/PvP paths, which is another reason to use provenance.

The existing player projectile caps/rate budgets do not solve enemy AI/bullet
population. Higher enemy count may also multiply player on-hit/on-kill callbacks
and cause existing player limits to deny more emissions. Neither side should
change the other's counters or treat a denied attack as refundable work.
Co-loading an unmodified RaidRaid scaler and a new one could multiply HP/count
and duplicate additive bonuses; this needs an explicit overlap policy, not
assumed Harmony ordering.

Suggested incremental scope, pending user feature choices:

1. Foundation: exact beta contracts, server authority, immutable policy,
   floor/spawn provenance, lifecycle receipts, availability and read-only UI
   snapshots. Preserve ordinary native defaults.
2. HP plus additive enemy damage, with owned application and a defined reset.
3. Exact Festival compensation using actual scaled HP; reward compensation as a
   separate policy. Add instant distribution once native economics and failure
   semantics are covered, retaining the existing XP restriction boundary.
4. Count only after normal/miniboss spawn-family coverage, native active-cap
   interactions and explicit per-frame work limits. Include waves, deaths,
   spawner teardown and changing floors in contract tests.
5. Explicit per-AI pattern timers; then, if requested, Sapphire after verified
   final-clear/settlement/rejoin semantics and a separately approved reward curve.

Relevant tests for any future implementation are behavior contracts rather than
copying the decompiled methods: native initialization order, twice-fired spawn
hooks, failed/late floor resolution, identity reuse, setting/reset changes,
fractional reward conservation, party sizes 1/2/3/4/5+, XP block/Rabbit exemption,
recipient disconnect during distribution, retries after partial work, boss phases,
run restart and settlement before/after replication. Live stock-host/guest checks
and profiler/network measurements remain required; no such results are claimed.

The selected beta hooks/fields still exist: monster-type setter, the three
spawner families, DropEXP/DropItems, AttackEvent/HealPercent, Timer.time and the
native grant/SyncVar paths above. This is not evidence that RaidRaid's assembly-wide
PatchAll succeeds on beta; unrelated missing patch targets can prevent this
subsystem from being installed. Beta workshop/custom progression especially
invalidates a general assumption that every run fits six named floor slots.
