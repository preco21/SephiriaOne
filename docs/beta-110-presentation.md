# 1.1.0 beta presentation compatibility

Audited 2026-11-01 against the installed Steam `workshop-beta` build `26324456`
(app `2436940`). This covers shared Zombie/Tag role markers, the main Leaf
Explosion, impact/death presentation, Dark Cloud observers, shared potion FX and
respawn FX. It establishes static code/asset contracts and controlled fixtures;
it does not establish live guest rendering or audio.

## Artifact identity and scope

The installed game is managed Unity/Mono. REA `inspect_managed_artifact`
reported complete metadata with no issues. Identity evidence:

| Artifact | SHA-256 | MVID / evidence |
| --- | --- | --- |
| Assembly-CSharp.dll | `383A0D5EA9218B797D6EACD1F67964A73606BA50572B7AF2D10AC5357B621D01` | `ba32c2d4-ef71-4869-856b-49ec7aea12f4`; `ev_9e8b617b69204d441626b5c6a42390dd8722142ddd5ade9ddc403453e4d0ef7a` |
| Mirror.dll | `E0BB5E72F9BC63DD13CD344DE0AA4DA07900BA15C60B5C0DB283E6DBDF02906E` | `1713ff29-6422-43c4-b99c-3b1b9c54b435`; `ev_737cf03b29c6a244f1fab641ec7e2987f1c187d4d42d61dadb2953edd6d39382` |
| sharedassets0.assets | `7686A9DF8106CCEE855A67AE108B15B2E98690E85734DE80ABDCC4206B47CBAF` | UnityPy serialized resource inspection |
| globalgamemanagers | `98EB1513651AF3234C943B527C255AD0529759CAD88AAD987D500CF59AFA9C43` | UnityPy TagManager layer inspection |

REA metadata inspection does not decompile or execute the target. The method
findings below come from ILSpy's exported installed assemblies and Harmony IL
inspection in the compatibility runners. UnityPy 1.25.4, with local managed
type-tree generation, read the actual serialized prefabs and animations. Raw
exports, scripts and logs remain under `%TEMP%/sephiria-beta110`, outside Git.

`NativeAssemblyContract.MatchesPresentationPair` accepts only either complete
audited game/Mirror pair. The previous pair remains:

- Game: `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
- Mirror: `C391C91BA860C537692C185B1075F4219EC83FA71A11990A2B51C13876AB7440`.

Mixed pairs and unknown hashes fail closed. This is an addon compatibility
boundary; it is not evidence that different game versions can play together.
Runtime registration, component, animation and prefab checks still run. A later
binary or resource change requires another audit.

## Native audio changes

The beta introduces `SoundManager` wrappers around the existing FMOD paths:

| Native caller | Previous call | Beta call |
| --- | --- | --- |
| `BulletDestroyModule.CreateDestroyVisualOnClient` | `RuntimeManager.PlayOneShot` | `SoundManager.PlayOneShot` |
| `DarkCloudObject.Attack` | `StudioEventEmitter.Play` | `SoundManager.PlaySoundEmitter` |
| `PotionEffect.CreateDrinkVisual`, `PotionEffect_Regeneration.CreateDrinkVisual` | `RuntimeManager.CreateInstance` | `SoundManager.TryCreateSoundInstance` |
| `PlayerSpawner.HandleDieClientside`, `Animator2D_Basic.PlaySound` | `RuntimeManager.CreateInstance` | `SoundManager.TryCreateSoundInstance` |
| `UI_ReviveEffect.OnOpened` | `StudioEventEmitter.Play` | `SoundManager.PlaySoundEmitter` |

`PlayOneShot(EventReference, Vector3)` forwards the same event and world position
once and catches audio initialization failure. `PlaySoundEmitter` checks the
emitter, calls its `Play` once, and catches missing-event/initialization failures.
`TryCreateSoundInstance` checks a null event, creates one instance and returns its
validity; it does not start playback. The original callers still attach or set
3D position, start and release. Failure logging uses a missing-event set and an
initialization warning latch; these helpers do not call combat or inventory code.

The production guards accept the exact game-owned wrapper types and signatures
alongside the previous FMOD calls. They retain payload, channel, ownership,
position, field and call restrictions. `GamePresentationAudioTests` separately
checks the installed wrappers' forwarding, dependencies and exception types.
Missing native sounds may now be skipped by the game's own wrapper; a successful
send or a passing IL check does not prove audible output.

## Shared role marker and Leaf Explosion

The role carrier remains `CharacterDebuff_ChimDamage`, network asset
`4098245047`. Its inherited client startup/update and loop teardown are
presentation paths. Server expiry remains a real damage hazard, so the adapter
still disables the owned host component before spawning, guards owned expiry,
avoids native debuff registration, and uses real network destruction on cleanup.
`CharacterDebuff` changes only its optional start sound call in the audited
diff; the selected carrier has neither start sound nor start FX.

The main Leaf Explosion remains the `MoneyWhirlwind` prefab, network asset
`2358477907`. The adapter disables server collision/gameplay before network
exposure and bypasses `OnSpawn`/weapon initialization. Its one instance retains
the two native animators and carries twice the prefab scale in Mirror's initial
spawn message. It adds no duplicate sound playback.

The beta adds `rotateBodyToDirection`, a `bodyAngle` SyncVar and an
`OnStartClient`/SyncVar callback to `BulletMoveModule_BlinkToTargetPosition`.
`ApplyBodyAngle` returns before doing work when rotation is disabled. The actual
MoneyWhirlwind module serializes `rotateBodyToDirection=0`; the new
`ValidateBlinkRotation` check rejects an enabled option. It tolerates the absent
field on the previously audited stable build. Blink still inherits the empty
`ClientMove` method. The native tests exercise both false acceptance and true
rejection without instantiating Unity objects.

The beta's `Bullet` adds bounce masks and a blocker helper inside its existing
server branch. Collision, movement and damage queries remain server-only.
`MeleeCollision.Update` and `BulletBreaker.Update` retain their server guards.
The new `TopdownRigidbody` overlap constraints consume the existing terrain
contact filter, whose masks remain Tile/Cliff/Pit/Pit_Water. Default layer 0 is
excluded. The selected blast and inspected player hierarchy have no
`Rigidbody2D`; the blast has no unit, body hitbox or interaction component.
`Interactable` triggers require layer 13, and the light trigger checks the exact
camera observer object. The only other `AttackTrigger` class had no typed users
in the exported game-source search. These are bounded static findings, not a
claim about arbitrary third-party component changes.

Mirror's `InitializeNetworkBehaviours` retains disabled components.
`OnStartServer`, `OnStartClient` and serialization do not filter them by enabled
state. Stock client prefab spawning instantiates the registered original,
applies transmitted scale before startup callbacks, and does not replicate
host-side enabled/collider flags. Network destruction executes normal client
teardown. These facts remain essential to guest safety and are covered by the
native role/Leaf tests.

## Installed asset observations

All coordinates below are build-local path IDs in `sharedassets0.assets`.

| Resource | Observed serialized contract |
| --- | --- |
| Chim carrier root `89021`, component `192528`, identity `205807` | Asset ID `4098245047`, scene 0; only Transform, NetworkIdentity, CharacterDebuff_ChimDamage. No start FX/sound, HUD or stack UI; null target/attacker and zero stack. |
| Chim loop root `88801`, animation `182766` | Only Transform, SpriteRenderer and Animator2D_SpriteRenderer; active/enabled, no event performer. `ChimFx`: 10 fps looping IDLE, the same ten `ChilrmFX` frames, no frame/sound/transform events. |
| MoneyWhirlwind root `59113`, Bullet `196539`, identity `191879` | Asset ID `2358477907`, scene 0; the same 18 components checked by production. All four hierarchy objects active on Default. Circle `139071`: non-trigger, radius 2.45, offset `(0, 0.83)`. No Rigidbody2D. |
| Blink `221162`, destroy module `225248` | Rotation disabled, angle range/offset zero; no fall/ground destroy. Exact immediate-destroy type with no sound, FX or fragments. |
| Main animation `182543`, ground animation `182513` | `MoneyWhirlwind` and `Lightning`: nonlooping 20 fps; 15 `DokkaeviExplosionFX00..14` frames and eight `SmallLightning_GroundFX0..7` frames. Frame 5 deactivates collision; frame 16 destroys. No ground events. |
| Main animation audio | Unchanged attached bomb GUID `(-1489583142,1090527874,1827617978,739549238)` at frame 0, and trade-money GUID `(-1768456097,1214824152,-1036708714,-221910872)` at frames 0, 2 and 5. |
| Native managers `229008`, `229009` | Each lists 2,342 spawn prefabs, including both carrier roots. Runtime still rejects a missing registration or custom spawn/unspawn handler. |
| Impact bullet root `58923`, destroy module `219504`, FX root `80912` | Base destroy module has one FX, no sound/alternates/fragments/random angle/scale. The exact SpriteFx `217218` automatically despawns without a transition override; its seven-frame animation has no events. |
| FlameBall root `58958`, destroy module `191967` | Base module is sound-only: GUID `(-33958705,1201911055,338028947,494537983)`, no FX/alternates/fragments. No projectile is spawned for the death cue. |
| DarkCloudObject `199892` | Retains an attack emitter reference `227913` and separate owner-area presentation. Native strike variants and the owner-only area ring are unchanged. |
| Regeneration effects `190664`, `214657`, `214658` | Share regeneration FX root `80822` and their drink/regeneration event references. Presentation remains independent of the healing method. |

## RPC and recovery boundaries

No presentation RPC signature or wire hash changed in this scope:

- Impact/death uses `DungeonManager.RpcBulletDestroyed`, hash `-1283831235`,
  with owner ID, transparency flag, prefab name, position, height, angle and scale.
- Dark Cloud uses `TargetCreateFx`, hash `-1744830929`, carrying position plus
  empowered/ice flags. The existing owner send is preserved; additional current,
  ready, same-floor observers receive the same cosmetic message. No strike is
  replayed. Cloud visual creation still precedes owner-only radius setup.
- Potion uses `PotionEffect.RpcCreateDrinkVisual`, hash `-1992013641`, carrying
  only position. It calls the virtual presentation method, never healing.
- Respawn uses `UnitAvatar.RpcCreateAdditionalLifeFx`, hash `-2080231604`, with
  no payload. The receiver uses the avatar world transform, spawns the pillar
  and opens the native short flash. The flash now uses the sound wrapper and
  still closes itself. Ordinary native revival does not already spawn the
  pillar. The addon emits it only after successful countdown restoration.

Death audio still excludes the victim's native local cue. Native
`PlayerSpawner.HandleDieClientside` retains the local-player gate around its
new sound factory and start/release calls. Shared-heal ambiguous-send failures
still do not retry healing, sounds or fallback particles.

## Verification and remaining limits

Before the fixes, the installed-beta report failed the potion audio guard,
Dark Cloud animation guard and role/Leaf fingerprint checks. The new rotation
regression failed until its production prefab guard was added. After the
changes, the coordinated non-deploying production build and compatibility
report passed (73 groups, zero failures), including the presentation mutation
checks for changed payloads, channels, ownership, gameplay calls and sounds.

Focused linked-source fixtures passed: RemoteAudio 44 checks, DarkCloudVisual
47 checks, ZombieVisual 36 checks and RabbitPotionHook 430 checks. Dark Cloud
reported zero allocations over 3,000 strikes to nine observers; stable role
updates reported zero allocations over 10,000 updates in their fixtures.

Reproduce the installed-code report after a non-deploying build:

```powershell
dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false -- `
  --compatibility-report `
  'C:/Program Files (x86)/Steam/steamapps/common/Sephiria/Sephiria_Data/Managed' `
  'SephiriaOne/bin/Release/netstandard2.1/SephiriaOne.dll'
```

No game was launched or addon deployed for this audit. No live host/guest
animation, collision, positional audio, mixer, missing-bank fallback, observer
delivery or recovery timing was exercised. Runtime pool/registration availability
and other addons can still disable these optional paths. The official
[modding rules](https://github.com/team-horay/sephiria_modding/blob/main/README.en.md)
were consulted; shipped assets and decompiled exports are not redistributed.
