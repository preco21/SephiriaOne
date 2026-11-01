# RaidRaid encounter investigation

Read-only investigation, 2026-11-01. Scope: additional boss patterns, fifth-floor
sequence, first-choice anvil, miniboss entry notice and encounter results. No
implementation, game launch, deployment, commit or multiplayer test was performed.
The desired port must work with only the host installing SephiriaOne. Boss
selection/fixing is excluded. The positional final-boss pin found inside the
extra-pattern helper is also excluded, rather than silently carried over.

## Evidence and limits

The inspected RaidRaid 3.324 artifact is
`C:/Program Files (x86)/Steam/steamapps/common/Sephiria/AddOns/RaidRaid/RaidRaid.dll`:

- SHA-256 `5ea5bddbb560f637636ea605172b9b4504cceb13bef085129a36723aa5787542`;
  MVID `3ea7e45e-d664-4e66-afc4-fe241d5c319b`; module name `RaidRaid_test.dll`.
- **R-enc**: REA `inspect_managed_members` parent
  `ev_bd13c9c444c0163d23bd22f6f37f38581cba61be379b45aa2a4375134dc93c33`.
  Selected 21 types: the nine `*Extra` helpers, sequence, anvil, announcement,
  results, boss-choice support, `BossExtraLock`, `RoomGuard`, `SpawnPacing`.
  247 methods; no unmatched selected type names; selected member coverage complete.
- **R-support**: parent
  `ev_5fe2e00c373ead094946b99aaa0e62d1f782deff003b2b7d0d40dd8cd157624c`.
  Selected result/announcement hooks, `QQQFallGuys_Patch`, `QQQBossSpawn_Patch`,
  `KrazSeen_Patch`, random-spawner tracking/hooks, connection cap, `BossRush` and
  `Settings`: 15 types, 259 methods, no unmatched type names.
- **G-enc**: beta build 26324456 `Assembly-CSharp.dll`, SHA-256
  `383a0d5ea9218b797d6eacd1f67964a73606ba50572b7af2d10ac5357b621d01`,
  MVID `ba32c2d4-ef71-4869-856b-49ec7aea12f4`; REA parent
  `ev_bca365523ca39f841653a6866420b9fcbfede31ef8b195432bb7faf241067575`.
  Twelve selected types include `StageEntity_Choice`, `FloorData`, `BossSpawner`,
  `Unit_QQQBoss`/AI, `Unit_LizardDemon`/AI, `Unit_BirdDemon`, Panther AI/shadow,
  `LibraryGuardZigzagGun`, and `MoleDrumStage`. 1,047 methods; no unmatched names.

Control flow was read in the existing private ILSpy export at
`C:/Users/preco/AppData/Local/Temp/sephiria-raidraid-investigation/main/RaidRaid`;
current game contracts were read at
`C:/Users/preco/AppData/Local/Temp/sephiria-beta110/game`. These are analysis
artifacts, not original authored source. Neither tree belongs in this repository.
The RaidRaid export has unresolved-reference/decompiler expressions in places;
exact constants/member anchors were corroborated through REA where used. The
selected-type REA coverage does not automatically include compiler-generated
coroutine bodies or prove live behavior. Both artifacts reported partial `#US`
coverage from string-terminal-flag issues while preserving readable strings.
Metadata tokens below are meaningful only for the exact RaidRaid digest/MVID.

A bounded source-name audit of 71 Harmony targets with the encounter-related
patch prefixes found direct current-beta declarations for all 71. It did not
validate every signature, enum meaning, IL pattern, serialized prefab, hook
installation order or guest animation. No result here certifies compatibility
with another game/RaidRaid build or stock-client rendering.

## Outcome and port boundaries

| Requested behavior | Static finding | Host-only assessment |
| --- | --- | --- |
| Additional boss patterns | Nine helper families, detailed below; not a universal extension to every boss | Native RPC/spawn basis is promising, but port per family after warnings, state and ownership validation |
| Fifth-floor sequential bosses | Kobes then Kraz, followed by the existing later Q progression | Native floor data and travel can carry this; requires owned-route identifiers and reward/progression guards |
| Guaranteed early anvil | Moves an existing anvil into the first branch row; creates one only when upgrade need and room/prefab conditions allow | Plausible with native floor data; an offered branch, not an unconditional entrance prop or forced visit |
| Miniboss-entry name notice | Names a player found in the trigger rectangle when battle begins | Native chat works on a listen host; current attribution/deduplication needs improvement |
| DPS/results | Encounter awards and totals, without an elapsed-time DPS denominator | Native chat is sufficient; bookkeeping and completed-event semantics need redesign |
| Boss selection/fixing | Separate `BossPick*` feature | Excluded; unnecessary for these features |
| Separate Boss Rush mode | Custom stage/preparation/reward mode with mod-specific client state | Not a direct host-only port; needs its own investigation/design |

## Additional-pattern inventory

The evidence for this table is R-enc and the named helper methods. These are
observed static algorithms, not observed combat sequences. Most entry points
check `Settings.BossExtraOn` and `NetworkServer.active`; those gates do not
provide run/floor/object lifetime ownership by themselves.

| Boss / native type | Additional behavior and notable bounds | Main native mechanism / port concern |
| --- | --- | --- |
| Mogdi / `Unit_MoleChieftain` (`MoleExtra`) | Adds two Odner cross-bombs to dynamite throws (1.5 s gate), five-shot fans on swings (0.1 s gate), five cross-bombs on windmill; hand stomp schedules two cross/X rock lines, each 2–40 points at 0.07 s spacing. Target rock every 2 s and five random rocks every 5 s, subject to stage/target/jump checks. | Stock bomb/punch prefabs through `Bullet.Pool.Spawn`; `MoleDrumStage.Hit` for rock warnings/attacks. Global boss/stage lookup can select the wrong simultaneously loaded arena. Due rock queue is drained without a per-frame budget. |
| Odner / `Unit_MoleBigBomb` (`OdnerExtra`) | Five radial shockwaves on landing (0.3 s gate); target dynamite every 3 s, eight random dynamites every 8 s; two horizontal sweeps every 15 s, each 2–40 positions, 0.04 s spacing. | Reuses Mogdi fire data, native dynamite prefab and armadillo shockwave prefab. `LineQueueTick` drains every overdue spawn in one update. Disabling skips `Tick` before queued state is cleared. |
| Rataka / `Unit_OinkKing` (`RatakaExtra`) | Replaces rockfall coroutine with three falls, four below 33% HP; queues 16 orbiting bullets every second rockfall sequence, then fires opposite pairs 0.15 s apart. One-in-five bullet-hell calls can become a 3 s counter-rotating windmill, eight bullets per 0.15 s firing step, at one-third standard damage. | Uses native bullets and bullet-hell RPCs, retains bullet/netId identity before destruction. Directly repositions stopped orbit bullets; guest synchronization must be observed. Custom replacement writes AI flags and steering speed, so cancellation must release owned state. |
| Lerid / `Unit_PantherRogueKing` (`LeridExtra`) | Adds five slow shuriken to second volley in normal pattern, two to basic attack, eight-direction burst after 0.72 s on Fury. Adds a third simultaneous dagger shadow or paired spread shadows. Changes random-dagger wait 1.4→0.8 s. | Native `CreateShadow`, `ThrowDagger`, `DaggerSpread` and bullet spawn. Temporary AI-state hold uses enum value 3 (`CloneDaggerSpread` in beta). Local `UI_AOEWarning_PantherArc` pool is raised to at least 24; a host-only change cannot raise an unmodded guest's pool. Pool pressure and overlapping warnings need guest testing. |
| Pantaxis / `Unit_LibraryGuard` (`LibExtra`) | On six ordinary patterns raises side guns, then fires three bullets from each at half standard damage after 0.6 s. Three-second cycle gate, ten-second exclusion around native zigzag. | `LibraryGuardZigzagGun.Up/Fire` synchronize gun state/animation and spawn stock bullets. Gun arrays come from global `FindObjectOfType`; pending fire does not recheck the option at execution. |
| Askard / `Unit_Askard_Chapter2`, `Unit_Askard_Chapter3` (`AskardExtra`) | Mirrors blood rain from the opposite side; chapter 3 follows laser/stomp with point tentacles (0.1/0.9 s delay), then a stomp after darkness ends, waiting at most 4 s for idle. | Reflects native blood-rain coroutine and calls `CreateTentacle_Point`, `RpcPlayPointSoundEvent`, `StartStomp`. Coroutine cancellation and scene ownership need explicit guards; fallback player selection lacks a floor check. |
| Kobes / `Unit_BirdDemon` (`KobesExtra`) | Four total rapid attacks (16 s limit), three alternating battlefield slashes (12 s limit), two extra circle emitters after 1.2/2.2 s. Secondary attack adds one falling bullet per other living player; every 8 s, three falls per living player; flight adds falls toward up to two players each second. | Native attack methods, falling-bullet data and network-spawned circle emitters. Nine living players imply up to 27 scheduled falls per rain cycle. Global alive-player lists have no floor/readiness filter. Clearing native shared-hit lists changes multi-hit semantics. |
| Kraz / `Unit_LizardDemon` (`KrazExtra`) | Pattern gap 0.5 s, 0.3 s below 50% HP; in/out delay 0.7 s; four dashes at 0.1 s intervals. Sword-sphere count clamps living-player count to 3–6; round-robin targets. Adds 7×7 sphere-end and 5×5 dash-target explosions, opposite second sunken pattern, follow-up in/out sphere after 0.5 s, random arena bands every 8/7/6/4 s at 100/75/50/25% HP tiers. | Reflects `CreateSwordSphere`, `RpcCreateSunkenWarning`, `CreateSunkenAttack`; uses native target-position SyncVar. `Has(int)` returns true regardless of the named mask. Global animation-speed override and original timing caches need lifetime ownership. Concurrent warnings share native warning state. |
| Final Qliphoth / `Unit_QQQBoss` (`QQQExtra`) | Tops up circle-echo history to at most six prior sets; adds three lasers to circle attack, four after body change, two per red-laser firing gate, three random lasers every 0.5 s during green action and one every 1.5 s during FallGuys. Issen schedules 22 native windmill emitters; red laser completion adds mixed lightning ring (stock count or fallback 16). Changes laser repeats/cycle; normalizes one pattern-list entry. | Native one-shot laser warning, server overlap attack and FX RPC, plus native network prefabs. Overdue laser/burst queues drain in one frame. One global boss reference, frame-level dedup and incomplete reset state cannot safely represent concurrent/reused bosses. |

Representative RaidRaid method coordinates: `MoleExtra.AddLine` `0x060001db`,
`OdnerExtra.LineQueueTick` `0x060001c4`, `RatakaExtra.StartOrbs` `0x060001ac`,
`RatakaExtra.WindmillTick` `0x060001b8`, `LeridExtra.PatchWait` `0x06000381`,
`LeridExtra.EnsureArcPool` `0x06000399`, `LibExtra.OnPatternStart` `0x060001eb`,
`AskardExtra.Mirror` `0x06000374`, `KobesExtra.RainAll` `0x06000324`,
`KrazExtra.OnBossServer` `0x060003e0`, `QQQExtra.HostTick` `0x0600020c`.

RaidRaid's `BossDb` supplies these full Korean labels for seven entries above:
두더지 대장 '까만손 모그디', 발파 전문가 '오드너', 약탈자 우두머리 '라타카',
그림자 검 '레리드', 도서관의 수호자 '팬택시스', and 클리포트의 수호 악마
'코베스' / '크라즈'. Askard and final Qliphoth are identified here by their
exact native types; their full localized display titles were not established.
The registry also contains 심식의 뿌리 '하즈'의 잔재 and 실성한 과학자 '에르마';
registry membership alone does not mean those bosses receive extra patterns.

The scoped attacks reuse native projectile/prefab, arena, warning and RPC
mechanisms; no newly shipped custom attack-art asset was established. Custom
server scheduling/AI changes still require synchronization checks. Lerid's pool
resize is a host-local visual-capacity change, Rataka's manual orbit transforms
need stock-guest observation, and QQQ's native FX dimensions remain guest-local.
Optional client installation for chat/profile UI does not relax the host-only
gameplay requirement for these cases.

This inventory does not establish enhancements for every game boss. In
particular, `Unit_QQBoss` detection/prefab reuse is not evidence of a dedicated
extra-pattern suite for that boss. HP/count/attack/tempo scaling and the separate
Boss Rush mode must not be conflated with the additional attacks above.

### Final-boss healing penalty and excluded pin

`QQQExtra.OnLaserHit` (`0x0600020a`) accepts successful hits with damage ID
`LastPatternOneShotLaser`, player victim, option on and host active. Unless
`DEBUFFIMMUNITY > 0`, it adds 75 to `HEALINGPENALTY` for 25 seconds; repeat hits
refresh the expiry instead of adding another 75. It spawns a stock poison
debuff for appearance, zeroing its damage/resistance reduction and stretching
its tick timer. Expiry destroys the FX and subtracts 75.

Beta `UnitAvatar.Heal` reads the same custom stat additively, then applies the
separate hard-mode healing penalty and clamps negative healing to zero. Thus
this can stack with other contributors to `HEALINGPENALTY`; the exact fox
contribution was not established from the selected code/assets. The helper
checks the damage ID but not the current boss origin. It also lacks explicit
run/reset cleanup for the stat delta; expiry only advances while server ticks
continue. A port needs a reversible owned contribution bound to the player
lifetime, independent cleanup of visual effects, and a deliberate decision on
whether native final-pattern lasers should receive the same added penalty.

`QQQExtra.HostTick` calls `LastPatternPin` (`0x0600020d`) even when
`BossExtraOn` is false. It records a final-phase position and writes the boss
transform back to that anchor after transformation/fast movement completes.
Omit this call, its anchor state, and its reflective field probes. None of the
added attack helpers above consumes that anchor; explicit position arguments
drive laser warnings and attacks. This is distinct from excluded `BossPick*`
room selection. Neither form of boss fixing is required for the requested port.

## Native guest transport and authority

Static beta evidence supports the following routes:

1. `ProjectileSpawner<T>.Spawn` instantiates a stock prefab, initializes owner,
   damage, factions and movement data, calls `NetworkServer.Spawn`, then
   `OnSpawnFinalized`. A method called `Pool` is not evidence of allocation-free
   object reuse: this beta method actually instantiates and destroys objects.
2. `LibraryGuardZigzagGun.Fire` sets a SyncVar, starts server bullet creation and
   sends `RpcFireAnimation`. `PantherRogueKingShadow.DaggerSpread` sets the
   server parent and sends a stock RPC; each guest makes its own warning arcs.
3. `Unit_LizardDemon.CreateSunkenAttack` initializes stock `WideAreaExplosion`
   owner/size and server damage/faction before network spawning. Its warning RPC
   transmits positions/sizes and follow target; the guest resets previous
   follow-warning state. Overlapping uses can replace a previous moving warning.
4. `Unit_QQQBoss.AttackLastPatternOneShotLaser` is server guarded, performs a
   native overlap box, deduplicates victims and calls `ApplyDamage`.
   `RpcCreateLastPatternOneShotLaserWarning` and `RpcSpawnLastPatternOneShotLaserFx`
   transmit position/angle to existing client receivers. Client width, length
   and warning duration come from client prefab fields. A host-local change to
   those fields is not a synchronized change to guest warnings.
5. `FloorData` has stock reader/writer support in `Mirror.GeneratedNetworkCode`;
   native generated-floor synchronization and registered room asset IDs can
   describe the proposed route without a custom guest message.
6. Listen-host `DungeonManager.Chat` sanitizes text then calls native `RpcChat`.
   It is a `[Client]` method, so server active alone does not support a headless
   server. Current beta caps a message at 120 characters and a name at 64.

These routes establish a host-authoritative implementation possibility, not
visual equivalence. Native observer delivery, warning visibility filters,
guest-local pools, spawn readiness, audio called locally by helper code and
animation events can still differ. `QQQBossSpawn_Patch` initializes the helper
from `OnStartClient` gated by server active, confirming listen-host assumptions.
No custom RPC type is needed for the scoped native-pattern/route/chat features;
registered prefab availability must still be validated before scheduling damage.

## Fifth-floor sequence: precise behavior and risks

R-enc: `BossSeq_Patch.Postfix` `0x0600007b`, `FindPair` `0x0600007c`,
`IsSeqFirst` `0x06000082`, `BossSeqDoor_Patch.Prefix` `0x06000083`,
`BossSeqReward_Patch.Prefix` `0x06000085`.

At `StageEntity_Choice.GenerateStage` postfix priority 200, the helper resolves
stage index 4 (falling back to `stageOrder - 1`), requires both bird and lizard
boss room prefabs, and requires `BossSeqOn`. It finds the furthest visible boss
node, retains the first node at that progress, removes other boss alternatives
at that progress and their incoming references, replaces the retained room
with Kobes, and appends a Kraz node at progress +1. It connects Kobes to Kraz,
copies selected floor metadata and clears Kraz's outgoing links. Later Q
progression is left to the ordinary next-stage flow. This is **Kobes → Kraz**,
not every boss in the game or a generalized boss-selection feature.

`Settings.GetBossPickOn(4)` returns false while sequence is on; logs explicitly
say the fifth-floor fixed selection is ignored. The boss catalogue/selection
UI and `BossPickCore.OnBossFloor` are unnecessary dependencies. Keep only the
native bird/lizard room identification needed by the sequence.

The first room's dramatic-death prefix disables `enableReward`, sapphire count,
tablet count and HP-restoration prop. The door prefix intercepts
`PlayerAvatar.ServerLoadStage`, waits for a proximity count, then uses native
`MoveFloorViaWorldmap(nextGuid, false, "FLOORSTARTING")` instead of next-stage
loading. This queues native shared floor movement; it is not a raw teleport.

Port blockers in the literal implementation:

- `IsSeqFirst` only tests visible boss plus a nonempty outgoing link. Door and
  reward hooks do not check option, stage, Kobes identity or route ownership.
  They could affect another addon/native boss topology even with generation
  disabled. Capture the exact generated route GUIDs and generation instead.
- Beta generation already handles `bossGenerateParameter == "ALL"` and
  `hardModeAllBoss`, expanding boss alternatives and setting hard modifiers.
  Deleting terminal alternatives can override that behavior. Decide explicit
  precedence; do not infer that two sequential bosses means native ALL mode.
- RaidRaid's `ChoiceExpand_Patch` also patches `GenerateStage` and expands route
  branches, unlike SephiriaOne's reward candidate `/choices`. Coordinate route
  transforms once, with validated ordering, rather than stacking postfix edits.
- Beta `BossSpawner.ServerEndBossDramaticDie` instantiates `rewardPrefabs`
  outside the `enableReward` block, and still handles doors, revives/progression.
  The four field edits therefore do not prove that every first-boss reward is
  removed. Authored room reward prefabs and addon kill rewards require review.
- The proximity gate counts all alive connections in its denominator and all
  nearby avatars in its numerator, including nearby dead avatars; no floor,
  loading, readiness or pending-transfer filter. It also bypasses the native
  `ServerLoadStage` UI/quest/custom-game eligibility checks. Preserve applicable
  native travel checks and avoid duplicate queued transfers.
- The first FloorData is mutated before every operation completes; a caught
  exception can leave a partial route change. Prepare/validate the transformed
  graph before publishing it. Validate save/resume and already-generated floors.

## First-stage anvil

R-enc: `AnvilFirst_Patch.Postfix` `0x06000044`, `Swap` `0x06000047`,
`Convert` `0x06000049`.

The option means a visible **anvil branch at nodeProgress 1 in stage 1**, not an
anvil in entrance node 0. If one already exists there, it does nothing. If a
later anvil exists, it swaps the room payload into a suitable first-row node,
retaining graph GUIDs/positions and connections. The swap includes prefab,
seed, event/threat, traveler/merchant, hidden-room/pocket, disturbance/hard,
quest, sepia, first-anvil reward and random-room metadata. It tries to avoid a
duplicate event at the old anvil's row and avoids boss/miniboss/special threat
values 5/6/7 for the selected first-row node. Duplicate avoidance is best effort:
the final `PickTarget` fallback drops the forbidden-event filter. The swap moves
one existing anvil instead of adding a second graph node. It guarantees neither
that every first-row branch is an anvil nor that the party chooses that branch.

If no anvil exists, it converts a first-row candidate only when `createAnvil`
is true and an anvil prefab can be found in unique/essential stage pools. If
everyone has finished upgrading (`createAnvil == false`), it does not create an
absent anvil; an existing later anvil can still be moved because that swap runs
before the `createAnvil` check.
No candidate, unsupported stage, missing prefab or exception also leaves it
unchanged. The preferred-event array was not fully reconstructed in this pass.

For a port, preserve the native upgrade-need condition unless a separate user
decision changes it. Validate event/prefab consistency and asset registration,
apply before generated-floor replication, and make transformation idempotent.
SephiriaOne's merchant route schedule may observe the moved content: keep
`FloorData` mutation confined to generation before route history is cached.
This feature has no dependency on fixed boss choice.

## Miniboss-entry announcement

R-enc: `MinibossRoomAnnounce.OnBattleBegin` `0x06000165`; R-support:
`CommonBattleBegin_Patch` and `EnemyBattleBegin_Patch` prefix
`ServerBattleBegin` and reflect pre-battle `battlePhase`.

It requires option on, host active, multiplayer count above one, phase 0 and a
spawn phase containing monsterType 1. It chooses the first non-dead avatar in
`PlayerSpawner.MultiplayerList` whose coordinates lie inside the spawner's
world-space trigger rectangle, then sends a localized native chat line naming
that avatar. Dedup is only the last cleaned name plus five unscaled seconds.

It announces **the player's name**, not the miniboss's name. It does not prove
who crossed the boundary first when multiple players were present, distinguish
same-name players, or verify floor/loading/connection lifetime. It is a prefix,
so it can announce before native battle-start success or another prefix veto.
Use a successful first-battle transition with one per-spawner/per-run receipt,
and label the chosen participant accurately if the true triggering player is
not available. Native chat suffices for stock guests. No per-frame scan is
needed; the event should consume one bounded same-floor roster snapshot.

## Encounter results, not a DPS meter

R-enc: `BossResultTracker.OnUnitSeen` `0x06000214`, `OnDamage` `0x06000218`,
`OnRevive` `0x06000219`, `Tick` `0x0600021c`, `Emit` `0x0600021f`.
R-support records the damage/revive/cinematic/player hooks.

The tracker starts on observed miniboss/boss registration. Bosses lacking AI
are retried up to eight seconds. It can group several tracked bosses into one
global active encounter; it polls at 0.25 s. After tracked deaths it waits
3 s for boss sessions, 5 s for miniboss-only sessions, 12 s for QQ/QQQ sessions,
and additionally waits for dramatic death completion with a 25 s escape limit.
Disappearance without a death drops the session without output.

Its five awards are:

- Destroyer: greatest boss/miniboss HP loss attributed to direct player
  `damage.origin`, with share of recorded total damage.
- Immortal: greatest actual player HP loss on successful non-system hits.
- Savior: most observed calls by a reviver to the direct/native-command revive
  paths. The receiver is the reviver, not the patient.
- Evader: fewest counted successful hits among players who got a stats record.
- Martial Artist: parry, perfect guard and dash-invincibility feedback count.

There is no encounter-duration field or damage/time calculation in this class.
The output is eight native chat messages (separator, title, five awards,
separator), not a continuous overlay. The bounded search for `DPS` in the
export found the Boss Rush training dummy reference, not a general DPS meter.

Literal-port accounting problems:

- Damage taken/defense uses a 50-unit proximity heuristic and returns true if
  all boss references are gone. There is no floor/run scope. Damage to any
  classified boss/miniboss is accumulated while active, not just a member of
  the tracked encounter. A quick next fight can merge with pending results.
- Only direct `PlayerAvatar` origins earn boss damage; companion and indirect
  ownership need explicit attribution. Players with no qualifying event can
  be absent, biasing the minimum-hit award. NetId/name alone is insufficient
  across reconnect, avatar replacement or run restart.
- Revive postfixes count even if patient was null/alive and the native method
  did nothing. Global last-ID/0.75 s dedup can also merge legitimate actions or
  fail to dedup interleaved actors. Count a confirmed patient dead→alive
  transition and its initiating player instead.
- `HookPlayer` uses anonymous parry/guard delegates and a `_hooked` set; no
  symmetric unsubscription path is visible in this helper. Existing live
  player objects may retain callbacks through addon/session transitions.
- `CurDamage` is one global value set before `ApplyDamage` and cleared after;
  nested damage calls overwrite it and an exception has no finalizer cleanup.
  Use a scoped stack/context if contextual defensive-action tracking is kept.
- HP-before/after spans other damage hooks and nested events; it can measure
  more than the local hit unless the capture boundary is selected carefully.
  Float totals and rounding are not a complete damage-accounting protocol.
- The chat cap can truncate joined winner names at nine players. Use bounded
  lines and stable roster identity; do not emit a chat line for each damage hit.

A future actual DPS option needs a separately specified start/end/pause clock,
participation window, phase aggregation and indirect-damage policy. Those
semantics cannot be inferred from the award labels.

## Integration with current SephiriaOne

Source comparisons are to the current repository, not an assumption that its
existing fixtures prove a new encounter feature works live.

- [`SessionSettings`](../SephiriaOne/Session/SessionSettings.cs) already owns
  start/stop, `ClearScope`, run generation and shared synchronization. Bind
  encounter work to current dungeon/save/generation/floor/boss identities;
  clear or retire state on return to title, new run, floor destruction,
  disconnect, feature disable and addon unload. Read desired settings through
  the session model rather than copying RaidRaid's file-backed static getters.
- The independently owned, contract-validated Harmony pattern in
  [`MerchantNativeHooks`](../SephiriaOne/Features/Merchants/MerchantNativeHooks.cs)
  is a better installation boundary than copying a broad patch set. Preserve
  unpatch ownership and isolate a missing optional boss contract from the
  entire mod. Avoid co-install duplicate extras with RaidRaid.
- [`ProjectileLimitRuntime`](../SephiriaOne/Features/ProjectileLimits/ProjectileLimitRuntime.cs)
  tracks player/companion ownership and intentionally passes native non-player
  owners through. Boss bullets, `WideAreaExplosion`, direct-overlap lasers and
  network emitters are not automatically limited by its existing settings.
  A separate bounded encounter-hazard admission policy is needed; silently
  consuming player budgets would change existing gameplay semantics.
- [`PvpCoverRuntime`](../SephiriaOne/Features/Combat/Cover/PvpCoverRuntime.cs)
  validates floor/run ownership and uses 16 search probes / at most two spawns
  per frame. Its lifecycle/receipt pattern is reusable, but cover occupancy and
  encounter attacks serve different purposes. Existing final-boss cover can
  coexist with boss metadata: extra dashes, arena bands, explosion regions and
  authored entry/exit anchors need coexistence checks. Do not remove progression
  protections or pin a boss to work around geometry collisions.
- [`MerchantRuntime`](../SephiriaOne/Features/Merchants/MerchantRuntime.cs) and
  [`MerchantRooms`](../SephiriaOne/Features/Merchants/MerchantRooms.cs) retain
  run/floor ownership and exclude boss rooms for placement. A sequence rewrite
  changes the route but does not itself authorize merchants inside boss rooms.
  Ensure anvil/route edits are complete before merchant schedules observe them.
- PvP/result hooks already touch damage, revival and lifecycle. New encounter
  capture must exclude unrelated PvP/merchant damage by explicit scope and
  compose with existing prefixes/postfixes; wrapping every global damage event
  with allocation-heavy roster searches is unnecessary.

## Supporting systems and omitted mode

`RoomGuard` is **lobby hygiene**, not physical encounter-room protection. It
reads Steam lobby `MaxMembers` (fallback `Settings.MaxPlayers`), disconnects an
excess new connection in `HorayNetworkManager.OnServerConnect`, and leaves an
old lobby before new-room creation. Do not import its explicit disconnect or
leave behavior merely to support extra boss patterns.

`RandomSpawnerTracker` supports monster scaling: watches at most 64 random
phase spawners, scans newly appended entries every 0.25 s and sends them to
`ScalerRunner.Register`. It records original additional limits, then scales
the concurrent limit by max(normal,miniboss count), except floor six uses normal
count. Count-only append tracking can miss same-length list replacement;
dictionary/set threshold clears are not lifetime-safe ownership.

`SpawnPacing.Accelerate` wraps `RandomEnemyPhaseSpawner.SpawnEnemy` and divides
only yielded `WaitForSeconds` durations in `(0, 0.15]` by the normal-monster
count multiplier. It accelerates spawning; it is not a frame-time budget. It
also catches `MoveNext` exceptions and terminates the wrapper, potentially
hiding incomplete native spawn progression. Treat count, concurrent-active
limit, and spawn-per-frame budgets as independent controls.

The separate `BossRush` mode creates preparation/mini rooms, tracks per-player
progress, rewards and route chains, and patches many item/travel/UI systems.
`SendPrep` sends `AugMsg(kind=8)` through `AugNet.ToClient`; `ForEachClient`
filters on `Augment.IsModded`. That custom client state is a concrete barrier
to simply promising stock guests the complete mode. Only this boundary and its
separation from `BossSeq` were investigated here; its full port remains out of
scope, including custom room materialization and all reward/interaction paths.

## Remaining validation before implementation is declared ready

The investigation answers what the requested features currently do. Actual
host-only compatibility, assets and performance remain partially answered.
Required follow-up evidence should include:

1. Exact current signature/IL/enum checks for each enabled family, native prefab
   asset IDs, client receiver timing and disabled-option cleanup. Audit the
   coroutine compiler bodies separately where a transpiler is necessary.
2. One host with unmodified guests, including late join/reconnect and avatar
   replacement; guest-visible warning precedes every added hit. Specifically
   test Lerid arc pool pressure, Rataka orbit motion, Kraz follow warnings and
   final-boss laser/poison appearance. No positional pin or fixed boss-choice
   setting should be installed.
3. Nine participants and an intentionally stalled/low-FPS host: cap overdue work
   per frame, avoid one-frame catch-up hazard bursts, retain warning-to-hit
   intervals and finish progression. Measure frame time, allocations, active
   hazards and spawn/network rate; no current benchmark supports a performance
   claim for these patterns.
4. Death mid-pattern, wipe, cancellation, floor travel, game restart, run/save
   reload, title return, addon reload and option off/on. Verify owned timers,
   AI holds, callbacks, hazards, healing penalty and pending results retire.
5. Fifth-floor native random/ALL/hard-mode routes, sequence on/off, duplicate
   generation hooks and saved modified routes. Verify no duplicate reward,
   retained intended final reward, both room exits and subsequent Q progression,
   with cross-floor/loading/dead party members and no duplicate travel request.
6. First-stage anvil with an existing early/later anvil, upgrades complete,
   no eligible candidate and missing/unregistered prefab; retain graph integrity
   and merchant schedule behavior. Announcement and result tests must validate
   completed native events, same-name players, zero-contribution players,
   indirect damage and 120-character chat bounds.

No live-success or measured nine-player-performance claim is made by this note.
