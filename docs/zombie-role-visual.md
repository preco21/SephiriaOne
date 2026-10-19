# Zombie role indicator

Since `0.66.2`, Zombie Survival gives each current Zombie the native
**looping green X-shaped mark** (`BuffFx_Chim`) centered on the character.
It replaces the small Poison particles introduced in `0.65.0`. The marker
follows movement and remains through death/pending conversion without adding
chat messages, actual debuffs, damage, sound, stat changes or a status HUD icon.
The audited native transport supports host and unmodified guests; live combat
readability remains unverified. See the [implementation follow-up](#green-x-implementation-0662-2026-10-19).

## Original Poison selection and shipped evidence (0.65.0)

The installed Poison effect is a small sprite-particle loop, without a startup
sound or startup burst prefab. Its main particles have a 0.25-second lifetime,
0.3125 size, maximum 20 particles and base emission of 4 per second (native
`ElementalParticle.SetMatrix` scales emission to actor area). This preserves the
character silhouette rather than covering it with an opaque overlay. Exact
combat readability and apparent color require live inspection; the damage-text
color is not evidence of the particle texture's color.

`sharedassets0.assets` evidence:

| Object | Native identity |
| --- | --- |
| Poison network prefab | GO 87247, `CharacterDebuff_Poison` component 186524 |
| Network identity | Component 199822; assetId **1290287067**, sceneId 0, serverOnly false |
| Production network manager | Component 222029; `spawnPrefabs[289]` references exact GO 87247 |
| Particle root | GO 53946 `Poison_Charm`; Transform + ElementalParticle |
| Particle children | GO 74436 `Fx`, GO 91048 `Burst`; Transform + ParticleSystem + ParticleSystemRenderer |

The prefab root contains only Transform, NetworkIdentity and
CharacterDebuff_Poison. `startFxPrefab` is null, `startFxEvent` is empty,
`HUD_ID` is empty, and target/attacker/stack are unset. The loop contains no
collision, damage or other script components. Native stack UI is conditional
on an attacker, which the owned carrier never has.

Directly instantiating `SpriteFx`, `BuffFx`, ScriptableFx or overlay sprites is
local-only. A registered native buff/debuff carrier can instantiate its own
fixed visual on stock clients; see the follow-up below.
`SkillController.RpcCreateCastFx` supports a named effect but also owns the
player's shared casting-circle/charge handles; using it as an
infection marker could interrupt normal casting.

## Presentation and gameplay boundary

`ZombieRoleVisuals` creates its own clone of the registered native prefab. It
disables the cloned **component**, leaving the GameObject active for Mirror's
observer handling, before network exposure. It owns the clone before Spawn and
suppresses `CharacterDebuff.OnStartServer` and, since `0.66.2`, gameplay
`CharacterDebuff.Destroy` only for that owned instance. The
component's disabled state prevents its automatic server Update, HUD updates
and native death/expiry path. The Chim clone's unused impact FX/sound and bonus
stat ID are cleared as defense in depth; its hardcoded expiry damage is blocked
by the owned-only Destroy guard. The original prefab remains untouched.

It never calls `ApplyDebuff`, `InitializeAndSpawn`, `ApplyStatus`, or registers
the carrier in the avatar's gameplay debuff collection. Ordinary Chim, Poison and
normal cleansing therefore remain independent of this role marker.

Mirror invokes network start callbacks and serializes components even when they
are disabled. `CharacterDebuff` synchronizes only stack, target and attacker;
`Behaviour.enabled` is not transmitted. Guests instantiate the original enabled
prefab and run its audited client-only loop positioning. On the host the shared
match tick invokes only native `OnUpdate_Client` through a cached delegate.
No new frame controller, per-frame reflection, network transform or snapshot
stream is introduced. Initial observer state includes the target; a delayed
target spawn can resolve through the native SyncVar getter on later updates.

## Ownership and cleanup

- One marker per exact player/connection receipt and match token. Initial role
  selection and infection create it; death/respawn do not remove a current role.
- Readiness loss removes the unavailable target's carrier. The existing match
  loop recreates it when ready, and also reconciles destruction by native
  `HorayNetworkManager.ClearNetworkObjects` during transitions.
- Match conclusion, stop, selection/master-toggle edits, connection replacement,
  run replacement, authority loss and unload use existing match cleanup.
  A rejoining player cannot inherit a former connection's marker.
- Cleanup uses **NetworkServer.Destroy**, rather than ending a gameplay debuff
  or unspawning to a pool. Stock client destruction invokes `CharacterDebuff`
  `OnDestroy`, which stops, hides and destroys the owned loop. Network
  delivery is subject to normal transport latency and Unity end-of-frame cleanup.
- If native callbacks end the match during Spawn, retirement waits for Spawn
  to unwind, then destroys the completed identity. Unload keeps the startup
  guard installed while a spawn is still pending. Failed/ambiguous presentation
  stops retries until addon reload, logs once, and leaves match gameplay usable.

`ZombieVisualNative` checks complete game/Mirror code fingerprints once per addon
load. Each new carrier validates current prefab registration, exact type and
components, silent startup, target defaults, and absence of custom network
spawn/unspawn handlers. `NativeAssemblyContract` is shared with the existing
Play Tag explosion adapter; the hashing behavior is unchanged.

## Evidence scope and verification

Static inspection used REA managed metadata plus ILSpy 9.1.0.7988 and UnityPy
asset extraction. Game assembly MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.

```text
Assembly-CSharp SHA-256:
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
Mirror SHA-256:
C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440
sharedassets0.assets SHA-256:
73135788932F461054DA76FDE2BBA1793C64748E77E9DFA3028A1AB6DA47EE36
```

REA evidence identifiers: `ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`
(game inventory), `ev_c7ea5bd391df8b60cbd2f01d21a41257ca96a9d9799c96e189baec610cf73fab`
(selected debuff/visual members), and
`ev_331fb24e477b7df1c0ef2f30eb6dd11a31100f773a6f80334def6305c895bb8e`
(Mirror contracts). Metadata was complete; malformed terminal #US flags limited
some string decoding. Native sources/assets/private logs remain outside the repo.

Executable adapter tests cover owned-only startup suppression, normal Poison,
no server Update, follow/death/respawn, zero steady allocations, stale receipts,
native destruction/replacement, failed contract/instantiate/spawn/update,
reentrant stop/unload and cleanup failure. Runtime tests exercise actual match
creation/conversion/loading/reconnect/conclusion/interruption wiring. Installed
contracts check the native client/server split, serialized fields, disabled
component behavior, prefab spawn, loop positioning and network teardown.

Run the dedicated suite with:

```powershell
dotnet run --project tests/SephiriaOne.ZombieVisualTests -c Release -p:DeployMod=false
```

Verified for the original `0.65.0` marker: release build with zero warnings/errors; 32 executable visual checks,
2,704 runtime integration checks plus 69 Bat lifecycle checks, and 4,871
friendly-fire hook checks. Stable visual updates allocate zero bytes across
10,000 warmed observations. The complete portable/catalog/installed-game runner
passes, including all 33 compiled Harmony API references against installed
HarmonyX 2.9. Independent review found no actionable issue.

Also run the runtime, friendly-fire and installed-game/alternate-Harmony suites
from [general findings](general-findings.md). Live checks remain: host + stock
guest initial role and infection, movement/respawn, late observer, floor loading,
normal Poison alongside the marker, cure, mode stop/rejoin and unload. No game
was launched and no output was deployed for this change.

## More prominent marker investigation (2026-10-19)

Research against addon `0.66.1` and the same installed game fingerprint above.
**Historical research only; implemented in `0.66.2` below.** At the time, the small Poison loop was
still the active marker. Five-second round announcements do not control its
lifetime: it is already persistent independently of chat. Small particles can
nevertheless be hard to recognize among combat effects.

### Recommendation and alternatives

The strongest candidate inspected is the native **looping green X-shaped mark**
`BuffFx_Chim`. Replace the Poison carrier with a cosmetic-only Chim carrier,
using the existing receipt/match ownership and cleanup. It is a larger continuous
sprite animation rather than sparse particles, requires no font glyph, and
follows the target locally on each stock client without repeated chat or movement
RPCs. This is a proposed implementation, not a claim of live readability.

| Approach | Benefit | Limitation |
| --- | --- | --- |
| Native green X mark | Continuous green shape on the character; existing native target-following transport; no chat writes | Native debuffs can use the same visual; it is not a unique Zombie icon. Actual contrast/occlusion needs a live check. |
| Colored `[Z]` in replicated character name | Explicit, font-safe role label; native overhead name and character panels can render it | Stock Steam HUD refreshes remote names on alternate four-second ticks, roughly eight seconds. The owner's overhead label is not guaranteed visible. Names also feed other UI and run snapshots, so this needs careful composition/restoration with the existing gradient publisher. |
| Other stock auras/icons | Sharp Eye has a head-area golden glint; Haste has white wings; Fanatic Flame has a larger flame aura | Their appearance already means a normal spell/status, and buff carriers need their own server/client safety audit. Host-only color/scale/prefab edits do not customize stock guests' local loops. |

Do not use the delayed nameplate tag as the only indication of conversion or
cleanup. Normal player chat bubbles still write history and last three seconds;
floating text still drifts/fades. Neither solves persistent identification.
Native ally arrows depend on faction relations and fade outside combat. Changing
real factions merely to acquire a marker would interfere with gameplay policy.

### Green X asset and transport evidence

UnityPy inspection of the shipped `sharedassets0.assets` resolved the carrier,
its registration, complete visual hierarchy, and the actual animation frames:

| Object | Identity / observed properties |
| --- | --- |
| Carrier | GO **87236**, `CharacterDebuff_ChimDamage` component **187141**, ID `CHIM_DAMAGE` |
| Mirror identity | assetId **4098245047**, sceneId 0, serverOnly false; `NetworkManager` component **222029**, `spawnPrefabs[281]` |
| Carrier components | Transform, NetworkIdentity, CharacterDebuff_ChimDamage only |
| Loop | GO **87021** `BuffFx_Chim`; Transform **99934**, SpriteRenderer **156390**, Animator2D_SpriteRenderer **220991** only |
| Animation | `ChimFx`, repeating `IDLE`, 10 frames at 10 fps; no frame events, sound events or transform attributes |
| Animated sprites | **47743**, **48995**, **40748**, **41284** (`ChilrmFX02`, `01`, `03`, `04`); 28x28 canvas, 16 pixels/unit; extracted pixels show a pulsing green X |
| Renderer | Enabled, alpha approximately **0.714**, sorting layer ID -1563213359, order -10, no mask interaction |
| Startup | No start FX, no start sound, no stack UI prefab, empty HUD ID; target/attacker null and stack 0 |

The serialized SpriteRenderer initially references `ChilrmFX05`; inspect the
animation's actual frames, not just that preview sprite. Its transparent canvas
is larger than its visible pixels. The loop root's serialized y=2 is **not an
overhead offset**: `CharacterDebuff.SetLoopFxPositionClientside` overwrites its
position with target position plus half actor height. This is a body-centered
marker, not an arbitrary overhead icon. The child offset on Sharp Eye is real,
but that is a different carrier/visual.

Chim inherits `CharacterDebuff.OnStartClient`, `OnUpdate_Client` and `OnDestroy`.
The client creates its loop, follows the synchronized target and destroys the
loop with the network object. Its animator changes sprites; the inspected
animation has no actions or audio. No gameplay Initialize/Apply call is needed
to create the cosmetic loop. The existing disabled-host-component/startup-guard
pattern can isolate the server side; native guests instantiate their unchanged
registered prefab with only client behavior active.

**Critical difference from Poison:** `CharacterDebuff_ChimDamage.DestroyInner`
normally applies damage and dispatches an impact FX/sound. A cosmetic carrier
must never enter `CharacterDebuff.Destroy`, `RequestEnd`, Initialize/Apply,
AddStack, or the avatar's real debuff collection. Keep automatic server Update
disabled, intercept owned startup before Mirror Spawn, and remove the object
with **NetworkServer.Destroy**, whose audited destruction path reaches
`OnDestroy` without invoking `DestroyInner`. Do not clear an actual Chim debuff
when removing the marker. Implement explicit regressions for this distinction.

### Proposed lifecycle and verification boundary

Reuse `ZombieRoleVisuals`' one carrier per current avatar/connection/match,
initial-role and infection creation, readiness recovery and shared cleanup.
Keep the current countdown cadence and one conversion notice. Change the
specific prefab contract and safety fields, not team policy or respawn timing.
Native clients follow the target without a repeated network position stream;
one loop per Zombie bounds object count. Keep validation out of the steady path.

Before shipping a replacement: validate current registration/no custom spawn
handler, exact loop hierarchy/animation, and code fingerprints; exercise normal
Chim alongside the cosmetic carrier; prove no expiry damage, impact audio,
status mutation or stale markers on stop/rejoin/respawn/unload. Retain the
existing reentrant-spawn cleanup tests. A host and unmodified guest must then
check visibility during movement/combat and cleanup latency, including viewer
FX settings and busy floors. Static assets cannot establish live readability.

REA assembly identity evidence remains
`ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`.
Focused carrier/animator/name-HUD/ally-mark member evidence:
`ev_e0507338a9e73c2bb7eb601ad02a0391d0c20f6c4caa66d5dd4a7da99c8d0c67`.
Selected member metadata is complete; unrelated malformed terminal string flags
remain explicit. Extracted asset trees and sprite previews stay outside Git.
Fifteen read-only shipped-asset assertions passed for carrier identity,
registration, silent startup, visual components, animation frames/events and
the unchanged asset SHA-256. Documentation whitespace checks passed. No
gameplay build/test or deployment was needed for this research-only change.

## Green X implementation (0.66.2, 2026-10-19)

The approved replacement uses the exact Chim carrier/animation identified above.
`ZombieVisualNative` validates its registration and complete visual hierarchy:
one Transform, SpriteRenderer and Animator2D_SpriteRenderer; no other scripts,
children or colliders. It also checks enabled state, renderer binding, absent
animation delegates/event performer, and the ten repeated `ChimFx` IDLE
keyframes at 10 fps with no frame actions, sounds or transform attributes.
Validation runs on creation, not on every update. A modified or incompatible
prefab fails closed once, leaving match gameplay available.

`ZombieRoleVisuals` retains the shared match/receipt lifetime. It owns a disabled
Chim clone before Mirror callbacks, assigns only the target, and never inserts
it into actual debuffs. The same owned-only Harmony predicate now guards both
server startup and gameplay `CharacterDebuff.Destroy`. This second guard
explicitly prevents an expiry request from entering Chim's hardcoded damage and
impact-sound path. Ordinary Chim/Poison still execute native startup and expiry.
The clone's unused impact prefab, sound reference and bonus-stat ID are cleared;
the registered original is never changed. Real removal still uses
`NetworkServer.Destroy`, not gameplay expiry. The guard remains installed while
reentrant spawning is finishing, including during unload.

No new messages, countdown cadence, timers, target policies or role settings
were introduced. The existing one-carrier-per-Zombie lifecycle handles initial
selection, infection, respawn, readiness loss/recovery, receipt replacement,
native network cleanup, match stop and unload. Guests instantiate their stock
Chim prefab and follow the synchronized target locally; no new visual asset,
custom packet, network movement stream or guest addon is needed by this path.

Verified: `0.66.2` Release build with zero warnings/errors; 36 executable visual
lifecycle checks, 3,163 runtime integration checks plus 69 Bat lifecycle checks,
and the portable/catalog/installed-game suite including all 33 compiled Harmony
API references against installed HarmonyX 2.9. The expiry-guard regression and
Chim identity contract both failed before implementation, then passed. Stable
marker updates still allocate zero bytes across 10,000 warmed observations.
Read-only shipped-asset checks confirm active prefab/renderer/animator bindings
and all ten animation frames match the replacement validator. Independent
review found no actionable code regressions.

Live checks remain necessary: host and stock guest should observe each other's green mark
while moving, dying and reviving; stop/rejoin must remove it; ordinary Chim and
Poison must retain gameplay behavior. The native Chim visual can also appear
from a real debuff, so it is not an exclusive Zombie symbol. No deployment or
game launch is part of this change.
