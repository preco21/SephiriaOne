# Play Tag investigation (2026-10-15)

Status: research and design preparation against addon `0.55.2`. Play Tag is not
implemented yet. Three gameplay choices have been submitted to the user; do not
treat the recommendations below as confirmed rules.

## Requested behavior

Add a temporary mode alongside Deathmatch and Tournament. A randomly chosen
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

Pending decisions:

| Decision | Submitted recommendation | Alternative |
| --- | --- | --- |
| Explosion outcome | End Play Tag after one explosion | Eliminate the loser and continue until one winner |
| Minimum-fuse allowance | Two timed holdings at three seconds, immediate explosion on third receipt | First minimum holding plus two further holdings, explosion on fourth receipt |
| Dead recipient's fuse | Begin after the victim's one-second respawn | Begin immediately when the kill transfers the bomb |

## Shared implementation direction

Extend `Features/Combat/CombatMatchRuntime` instead of introducing an independent
match controller. Replace its binary tournament flag with an explicit mode
identity so Deathmatch's status, expiry, buttons and stop command cannot
accidentally operate on Tag. Retain one shared authority/run scope, callback
depth, participant receipts, respawn queue, K/D/A and recovery owner.

Keep the bomb policy separate from Unity: current carrier index, previous
passer, integer fuse, deadline/arming state, per-entrant minimum-fuse holdings,
and a consumed explosion marker. Model transfer and expiry as explicit
transitions; consume an outcome before invoking native callbacks. Use injected
time/random selection for deterministic tests. K/D/A reset must not replenish
the bomb's finite allowances.

Freeze connection-lifetime enrollment at start, as Tournament does, rather than
allowing reconnects to renew a minimum-fuse allowance. A late join waits for the
next match. Selection edits and master-toggle operations use shared cancellation
and recovery. Stop on invalid carrier identity instead of assigning its old
deadline to a replacement connection. Preserve native-stat/PvP balance settings.

Use the same command dispatcher and a new tab with Start, Stop, fuse duration,
K/D/A reset and the shared participant editor. Only configuration is persisted;
carrier, deadlines, allowances and presentation are temporary. Configuration
changes apply to the next match, avoiding a hidden extension of the active fuse.
An initial 30-second fuse, configurable from 3 to 3600 whole seconds, is the
proposed default rather than an existing shipped setting.

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

No audited generic native RPC directly selects this main animation. A short-lived
stock `MoneyWhirlwind` network instance is a candidate for further work, not yet
a proven cosmetic solution. Its root has a non-trigger CircleCollider2D, Bullet,
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

A zero-damage projectile alone therefore does not prove cosmetic-only behavior.
Before exposing an instance, isolate server collision, owner/faction, callbacks,
damage and hit response; audit guest-side collider interaction, stock spawn
registration and deterministic cleanup on animation end, stop, scene change,
failure and unload. A host-only collider change is not automatically replicated.
Do not modify the reusable prefab. Exact main FX/sound transport remains an
implementation prerequisite requiring those checks and a live stock-guest test.

The [user-supplied Namu reference](https://en.namu.wiki/w/%EC%84%B8%ED%94%BC%EB%A6%AC%EC%95%84/%EB%AC%B4%EA%B8%B0%20%EA%B0%95%ED%99%94#%EC%A7%93%EA%B6%82%EC%9D%80%20%EC%9E%A5%EB%82%9C)
could not be fetched during this investigation. The resource identification above
comes from installed definitions/code/assets, not an independently retrieved page.
Decompiled exports and asset inspection files remain outside the repository.

## Verification needed for implementation

Cover fuse rounding/minimum and finite allowance exhaustion; successful and
invalid transfers; expiry coinciding with respawn; carrier death by a non-carrier;
native resurrection; reset/stop/selection/disconnect inside callbacks; old-run
and old-connection rejection; and cleanup with every participant dead. Exercise
direct, companion, reflection and debuff kill attribution through the shared
boundary. Retain existing three-second modes and zero-work idle behavior.

Before changes, the runtime fixture suite passes **1,998** integration checks plus
**69** Bat lifecycle checks; the Tournament scheduler passes **41,176** checks.
These baseline checks ran with `-p:DeployMod=false`. They do not verify Tag.
No game was launched or deployment performed. Exact host/stock-guest blast and
audio playback will still require a live test after implementation.
