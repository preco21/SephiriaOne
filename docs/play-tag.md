# Play Tag (0.57.0, 2026-10-16)

Host-controlled bomb tag shares the friendly-fire match infrastructure. Research
began against addon `0.55.2`; implementation follows the behavior-preserving
`0.55.3` optimization pass. The game was not launched or deployed during this
work; native-code/asset evidence and automated checks do not prove live guest
rendering or audio playback.

## Controls

Use the **Play Tag / 술래잡기** tab, or these host commands:

| Command | Action |
| --- | --- |
| `/one tag start` | Capture current participants and begin the three-second countdown |
| `/one tag stop` | Stop and recover pending dead participants |
| `/one tag duration 30` | Set the next match's initial fuse, 3..3600 whole seconds |
| `/one tag duration reset` | Restore the 30-second default for the next match |
| `/one tag resetkda` | Reset shared scores and matchup records without replenishing bomb chances |
| `/one tag chaser-refill on` / `off` | Toggle the carrier's refill multiplier, initially on |
| `/one tag chaser-refill x2` | Set refill multiplier, 0.1..10, at most two decimals |
| `/one tag chaser-refill reset` | Restore enabled, x2 |
| `/one tag chaser-damage on` / `off` | Toggle incoming PvP damage reduction, initially on |
| `/one tag chaser-damage 75%` | Set incoming damage percentage, 0..100, at most two decimals |
| `/one tag chaser-damage reset` | Restore enabled, 75% |
| `/one tag status` | Show mode/fuse and chaser settings |
| `/one save` | Save configuration; nondefault chaser settings use preset v26, never a live match |

The participant editor is shared with friendly fire, Deathmatch and Tournament.
An empty explicit selection includes everyone. Changing that selection or
touching the master friendly-fire toggle stops the current match. Deathmatch
and Tournament controls retain their own mode-specific start/stop behavior.

## Rules

A temporary mode alongside Deathmatch and Tournament. A randomly chosen
carrier must kill another participant to pass a bomb to that victim. All
participants retain ordinary friendly-fire combat, including attacks against the
carrier, and recover one second after death. Each successful pass changes the
next fuse to `max(3, floor(previousSeconds * 0.8))`. At the three-second minimum,
each player's finite additional-chance counter must prevent endless passing.

The carrier receives a remaining-time/instruction bubble. Expiry kills the
carrier, announces the victim and last passer in EN/KO, and plays Rag's Leaf
Explosion presentation for host and stock guests. A carrier who has never
received a pass needs a separate announcement without inventing a prior owner.
Normal kills still use existing K/D/A and private matchup notices.

Confirmed decisions:

| Decision | Confirmed behavior |
| --- | --- |
| Explosion outcome | End Play Tag after one explosion; recover pending players through shared cleanup |
| Minimum-fuse allowance | Two timed holdings at three seconds, immediate explosion on third receipt |
| Dead recipient's fuse | Begin after the victim's one-second respawn |

## Chaser advantages (0.57.0, 2026-10-16)

The Play Tag panel has independent **Refill (x)** / **충전 (배율)** and
**Incoming (%)** / **받는 피해 (%)** toggles and numeric Apply controls.
Both toggles start on, with x2 refill and 75% incoming damage. Turning an option
off preserves its configured number; its `reset` command restores enabled and
the default number. Values may be changed during a match without restarting its
fuse, resetting K/D/A or granting another burst of damage allowance.

Refill multiplies the global **damage allowance replenishment**, not HP recovery.
Global 10 becomes 20 with x2; the current global default of 70 becomes 140% of
victim maximum HP per game second. This option requires **PvP balancing** to be
enabled. Initial burst capacity, per-hit softening and artifact/debuff intervals
remain unchanged. Incoming 75% means 25% less otherwise-resolved player damage;
it also works when PvP balancing is off. Zero means no incoming player damage,
but does not disable native debuff/effect application.

`PlayTagChaserSettings` belongs to shared session policy. The common
`FriendlyFireRuntime.Scale` boundary reads `CombatMatchRuntime.IsActiveChaser`
at impact, using the actual current receipt, run, phase, living avatar and armed
fuse. Direct attacks, companions, reflection and debuffs retain the existing
attacker-owner rules. No native stats or guest addon state are written. Incoming
damage is reduced after global scaling, optional softening/budget reservation
and the native-safe numeric cap, before shield/HP consumption. PvE and other
match modes remain unchanged.

Transfer immediately removes the old carrier's advantages. The dead recipient
gains them only after successful revival arms the bomb. Dead, expired, stale or
disconnected holders cannot retain them, including before the next match tick.
Stop, reset, cancellation and new sessions use the existing shared cleanup.
Nothing needs to restore native stats because no applied stat bonus is stored.

Budget reservations remain in pre-reduction units. Each hit captures its incoming
factor; actual shield/MP/HP consumption is divided by that factor before refunding
unused allowance. This refunds genuine native rounding, veto, armor and overkill
losses without allowing rapid hits to refund away the intended reduction. A zero
incoming factor reserves neither damage tokens nor a Shock/Freeze damage pulse.

A monotonic chaser revision records role and setting changes. When a pair next
attacks after a change, elapsed refill is credited at no more than the base rate
(and the previous/current rates). Subsequent stable intervals use the current
multiplier. This deliberately conservative first interval avoids granting boosted
credit across an unseen A-to-B-to-A transfer. Existing token credit is retained;
configuration edits do not clear other combat clocks or create free bursts.

Verification: 3,514 executable combat-hook checks and 2,233 shared runtime checks
plus 69 Bat lifecycle checks pass, including extreme capped hits, saturated
rapid attacks, native shields, nested edits, directional reflection, companions,
transfers, expiry, stale receipts and run replacement. Portable coverage includes
253 Tag policy/preset checks and 3,505 catalog checks. Installed-code contracts
verify shared modifier/budget wiring and compact UI command dispatch. The warmed
real role query allocates 0 bytes across 10,000 calls; the warmed combat fixture
also allocates no per-hit state. The existing performance budgets pass: unchanged
five-player synchronization remains at 0 bytes/tick and participant snapshots at
2,264 bytes/refresh. These checks do not replace live Unity UI or
host/guest gameplay testing.

## Shared implementation

`Features/Combat/CombatMatchRuntime` owns the match. An explicit mode
identity keeps Deathmatch's status, expiry, buttons and stop command from
operating on Tag. The modes retain one shared authority/run scope, callback
depth, participant receipts, respawn queue, K/D/A and recovery owner.

`PlayTag/PlayTagBomb` separates the bomb policy from Unity: current carrier index, previous
passer, integer fuse, deadline/arming state, per-entrant minimum-fuse holdings,
and a consumed explosion marker. Model transfer and expiry as explicit
transitions; consume an outcome before invoking native callbacks. Explicit time
and initial carrier inputs support deterministic tests. K/D/A reset cannot replenish
the bomb's finite allowances.

Freeze connection-lifetime enrollment at start, as Tournament does, rather than
allowing reconnects to renew a minimum-fuse allowance. A late join waits for the
next match. Selection edits and master-toggle operations use shared cancellation
and recovery. Stop on invalid carrier identity instead of assigning its old
deadline to a replacement connection. Preserve native-stat/PvP balance settings.

Use the same command dispatcher and a new tab with Start, Stop, fuse duration,
K/D/A reset and the shared participant editor. Only configuration is persisted;
carrier, deadlines, allowances and presentation are temporary. Initial-fuse configuration
changes apply to the next match, avoiding a hidden extension of the active fuse.
The default initial fuse is 30 seconds, configurable from 3 to 3600 whole seconds.
The first carrier is selected uniformly from living, ready captured entrants.
Successful kill completion must precede expiry; a death callback that finishes
at the deadline cannot rescue an expired bomb. Killing an already-armed carrier
does not reset or pause their timer. Only receipt of a transferred bomb waits
for actual successful revival. At the minimum, an initial three-second fuse
counts as that carrier's first timed holding.

## Native death and attribution evidence

Installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.

`PlayerAvatar` inherits `UnitAvatar.Die(int, DamageInstance)`. On the server,
`Die(5, null)` on a living carrier sets synchronized HP/dead state, clears delayed
HP restoration, runs native death callbacks and sends `RpcDie`. The extra-life
branch is in `ApplyDamage`, before `Die`; direct death bypasses shields,
invulnerability and extra-life consumption without rewriting their state.

Do not use `ForceDie`: it bypasses the existing `Die` prefix/finalizer and also
destroys buffs. Do not simulate lethal damage: it invokes damage/defense/proc
semantics and might consume an extra life instead of losing the bomb round.
Passing null damage avoids native kill procs and MP-on-kill. The bomb explosion
announcement can name its last passer without fabricating an ordinary PvP hit
or awarding an unsupported K/D/A kill.

The existing `FriendlyFireNames.BeforeDeath`/`FinishDeath` hooks own
`CombatMatchRuntime.EnterDeath`/`LeaveDeath`. Keep enrollment and match scope
active when calling native death so `ProtectDeath` prevents unintended all-dead
settlement. Do not enter the hook manually a second time. Stops inside native
callbacks defer recovery until the death RPC has completed.

Evaluate expiry before due respawns. If the carrier is already dead, resolve the
logical explosion once without a second `Die`, a revive-then-kill, a fabricated
life, or another K/D/A event. A death callback can itself revive the player;
recording the consumed explosion independently prevents that from undoing the
round outcome. A global revive veto would be unsafe: native Resurrection also
changes faction and leader after invoking revival.

For transfer, extend the existing captured death notice with killer/victim
connection receipts and the match token. Notify the match only after a successful
`FriendlyFireKda.CompleteDeath`, normal native completion, current receipts/epochs
and a still-dead victim. Keep the notification before optional chat delivery.
Do not infer a transfer from polling `Died`, which also observes environmental
deaths, or from text in the kill log. Repeated, exceptional, stale or extra-life
deaths must not transfer the bomb. Existing K/D/A's handling of a confirmed death
followed by a native exception should remain unchanged.

Revival notices capture a dead-at-entry player receipt and match token. Only
successful completion of that admitted native call may arm the transferred
fuse. Nested no-op revivals cannot arm it early; exceptional completion stops
only the matching current Tag state. This avoids both an indefinitely unarmed
bomb and a fuse started before the native revival RPC completes.

Do not invalidate all combat receipts on a pass while participant hostility is
unchanged: that clears life contributions and can invalidate an outer hit when
a reflected/nested hit kills someone. Parameterize the shared respawn delay;
retain three seconds for Deathmatch/Tournament and use one second for Tag.

`PlayerAvatar.CreateChatBubble` destroys the previous bubble before creating its
replacement. Use the existing native avatar-chat transport and replace stale
carrier instructions when passing/stopping. No guest addon is needed for this
native text path. Asset/code inspection is not live guest-rendering evidence.

## Leaf Explosion resource investigation

The installed weapon definition matches the user-supplied description:
`Weapon_GreatSword_ClubAdvanced` uses `WeaponSimple_GreatSword.moneyWhirlwind`,
with a `MoneyWhirlwind` bullet and a `7.5` damage multiplier.

The main bullet's `AnimationSet` uses `DokkaeviExplosionFX00..14` at 20 fps;
its animation events contain the explosion sound layers. This is distinct from
`MoneyWhirlwindFx`, the bullet's successful-hit effect, which uses
`DokkaeviAirHitFX` sprites and `event:/Scene/crashProjectile` audio.
`DungeonManager.BroadcastAttackBullet` / `RpcAttackBullet` can transport the hit
presentation without a projectile, but that is not the requested main explosion.
Do not substitute it and claim the exact Leaf Explosion effect was implemented.

Exact installed identities:

| Resource | Identity |
| --- | --- |
| Weapon localization | `WeaponGreatsword_ClubAdvanced_Name` = Mischievous Prank; `WeaponAction_Greatsword_MoneyWhirlwind` = Special Attack: Leaf Explosion |
| Attack data | `Weapon_Greatsword_Whirlwind_Money(B)`, sharedassets0 `179217`; one physical bullet at 7.5x |
| Main prefab | `bullet/moneywhirlwind`, GameObject `58406`, Bullet `190991` |
| Native network prefab | NetworkIdentity `186508`, assetId `2358477907`, sceneId 0 |
| Main animation | Animator `217265`, AnimationSet `177271`; `DokkaeviExplosionFX00..14` |
| Ground layer | Animator `205750`, AnimationSet `177246`; `SmallLightning_GroundFX0..7` |
| Main blast sound | Frame 0, `event:/Scene/bombDemonExplosion`; GUID fields `(-1489583142,1090527874,1827617978,739549238)` |
| Leaf sound layers | Frames 0, 2 and 5, `event:/System/tradeMoney`; GUID fields `(-1768456097,1214824152,-1036708714,-221910872)` |
| Separate weapon swing | `event:/Scene/SpellBlast`; not part of the projectile animation |

The game's `Const` resource sets `greatswordMoneyWhirlwindCost=30`; native
spending occurs in `WeaponSimple_GreatSword.SubAttackButtonUp`. Presentation must
not invoke the weapon path. `Animator2D_Basic.PlaySound` creates, attaches, starts
and releases the main animation's sound events. The explosion is a 3D one-shot;
the leaf layers are 2D one-shots. Native animation repeats those leaf sounds
intentionally; an adapter should not add its own duplicate local playback.

No audited generic native RPC directly selects this main animation. The adapter
uses a short-lived stock `MoneyWhirlwind` network instance. Its root has a non-trigger CircleCollider2D, Bullet,
blink movement, immediate-destroy module, animation transition controller,
network transform, rendering metadata and top-down renderer. Its children hold
the two sprite animators/renderers and a sorting group. No UnitAvatar, hitbox or
Rigidbody2D was found in that hierarchy.

`Bullet.OnSpawn` resets collision, invokes callbacks and requests local camera
shake. `Initialize` assigns damage/owner/factions and can register with the
owner's bullet list. Server `Update` queries collisions. Frame 5 deactivates
collision and frame 16 invokes destruction; the movement base also has a
three-second lifetime. The destroy module has no extra explosion-damage override.
Native bullet breakers and melee collision can still observe a Bullet collider.

`PlayTagExplosion` directly instantiates the native prefab, then disables its
server Bullet/collider and clears owner, factions, damage and callbacks before
`NetworkServer.Spawn`. It bypasses `OnSpawn`/`Initialize`, preserving the two
native animators and their embedded sound events. It never modifies the reusable
prefab or adds a second local sound call. The native animation destroys the
instance at frame 16 (about 0.8 seconds); an unscaled three-second fallback covers
interrupted animation. Natural match completion preserves that brief cosmetic
lifetime. Explicit interruption, new matches, run/floor changes and unload clear
the owned instance. Only that presentation can outlive the match; no bomb,
respawn, score or hostility state does.

Guest collider flags are not replicated. Safety therefore depends on audited
stock behavior: guest `Bullet.Update` does no collision/damage query, Blink's
client movement is empty, melee and bullet-breaker queries are server-only, and
player movement filters Tile/Cliff/Pit layers rather than the Default-layer
cosmetic. Neither the explosion nor the inspected player hierarchy has a Unity
Rigidbody2D. Other inspected triggers require the interaction-checker layer or
an exact observer object. Both stock NetworkManager prefabs register this asset;
host startup registers their native spawn list with `NetworkClient`.

The adapter pins the installed game fingerprint above and Mirror SHA-256
`C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440`.
It also validates stock registration, absence of custom spawn handlers, exact
component hierarchy, layers, colliders, animation frames/events and sound GUIDs.
Validation uses serialized references or `GetComponent` on prefabs, never an
Awake-only `NetworkBehaviour.netIdentity` cache. A changed contract suppresses
the optional presentation and logs a warning; it does not spawn an unaudited
attack object or block match recovery. A future game build requires a re-audit.
Raw serialized checks passed for the installed asset; live host/guest animation,
audio timing and Unity object behavior still require an in-game test.

The [user-supplied Namu reference](https://en.namu.wiki/w/%EC%84%B8%ED%94%BC%EB%A6%AC%EC%95%84/%EB%AC%B4%EA%B8%B0%20%EA%B0%95%ED%99%94#%EC%A7%93%EA%B6%82%EC%9D%80%20%EC%9E%A5%EB%82%9C)
could not be fetched during this investigation. The resource identification above
comes from installed definitions/code/assets, not an independently retrieved page.
Decompiled exports and asset inspection files remain outside the repository.

## Base mode verification (0.56.0)

The pure bomb runner passes **20,680** checks, including rounding, invalid inputs,
exact expiry, delayed arming and randomized finite minimum-fuse play. The shared
Tournament scheduler retains **41,176** passing checks. Runtime fixtures cover
the shared controllers, duration capture, commands/localization, one-second
respawns, all-dead recovery, nested callbacks, stale connections/runs, score
reset, selection/master edits and presentation cleanup. Real Harmony hook
fixtures cover direct, companion, reflected, Burn and Shock kill attribution.
The final shared runtime run passed **2,154** integration checks plus **69** Bat
lifecycle checks; the combat hook runner passed **3,479** checks. Independent
review reproduced the nested no-op revival fault before the receipt/token fix
and verified that the corrected regression passes.

Installed-code compatibility checks validate native death/revival, stock chat,
network spawn/collision boundaries and exact animation/audio contracts. Mutation
checks reject changed assemblies and altered animation/sound events. Full preset
and catalog checks include EN/KO, v25 round trips and older schema rejection.
Production Release builds and these runners use `-p:DeployMod=false`.

The existing allocation probe remains at **0 bytes per unchanged synchronization
tick**, **0 bytes per successful resource plan**, and **2,264 bytes per five-player
participant snapshot**. Tag's unchanged active ticks are also checked for zero
allocation in fixtures. No coroutine, extra per-player controller or broadcast
per frame was added; carrier bubbles update only when the shown second changes.

No game was launched or deployment performed. Exact host/stock-guest blast and
audio playback remain unverified in a live session. An external native revival
that fails after partially mutating its player stops Tag safely; the addon cannot
claim to repair an interrupted native revival RPC.
