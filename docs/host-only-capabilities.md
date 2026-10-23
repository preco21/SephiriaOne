# Host-only gameplay and presentation capabilities

Investigated 2026-10-23 against installed Sephiria `1.0.33`, with addon source
`0.90.3` / `5b99a6b`. This is a capability survey, not a new implementation or a
live multiplayer test. No game launch, deployment, or game-asset changes occurred.
Use it with [presentation compatibility](presentation-compatibility.md) and the
[shared synchronization guide](synchronization-guide.md).

## What can reach an unmodified guest

The useful boundary is the stock client's receiver, not whether the host can
edit a Unity object. There are four different mechanisms:

| Mechanism | What the host changes | What guests receive | Late join / persistence |
| --- | --- | --- | --- |
| Native synchronized state | A supported SyncVar, collection, or registered network object through its native server path | Serialized values, interpreted using each client's installed code/assets | Initial state can reach new observers; native reset and initialization paths still matter. |
| Native presentation event | An existing RPC, with its existing payload and stock asset selectors | A request to play a known effect, sound, animation, or message | Usually transient; old RPCs are not a retained state snapshot. |
| Host decision, native outcome | Damage rules, grants, encounter selection, XP admission, match logic | Normal native HP, inventory, movement, spawn/despawn, etc. | Policy stays on the host; reconstruct current outcomes for new connections where necessary. |
| Unsynchronized local edits | Renderer material, texture, particle configuration, new item definition, local UI | Nothing automatically | Needs a suitable existing native field/message, or changes on the viewing client. |

All positive findings below assume compatible stock game builds and native
network readiness/observer visibility. A generated sender and receiver establish
transport support, not proof that a guest saw or heard the result in gameplay.

## Appearance and character size

| Modification | Visible to stock guests from the host? | Mechanism | Limitations / synchronization risks |
| --- | --- | --- | --- |
| Root size set before an avatar's network spawn | **Supported by the native spawn contract** | Mirror's spawn envelope includes root scale independently of transform updates | Scales rendering and colliders, not just visuals. Needs a safe pre-spawn hook and tests of owner movement, native scale bonuses, death and replacement avatars. |
| Change an already-visible player's root scale | **Not through ordinary transform replication** | Shipped player `NetworkTransformReliable.syncScale` is false | Host-only writes leave existing observers unchanged. A later observer can receive the new scale and disagree with old observers. Restoring only on the host has the same problem. |
| Live scale through native spawn-message replay | **Experimental possibility, not a ready feature** | The stock client can reuse an existing netId and apply the spawn envelope's scale | Also touches position, flags, initial-state deserialization and callbacks. Do not republish spawns as a scale shortcut without a dedicated lifecycle audit. |
| Scale only the sprite/body | **No audited general transport** | Local child Transform | Native body-height updates reset its scale; animator tracks can also modify children. Does not create a guest-visible cosmetic-size channel. |
| Select an existing costume | **Yes** | `PlayerAvatar.EquipCostume` / `LocalEquipCostume` publish costume and skin SyncVars | Changes stats, starting items, equipment effects and weapon restrictions as well as appearance. Use valid stock costume/skin pairs. |
| Select an existing skin for the current costume | **Yes, narrower than a full costume change** | `NetworkcurrentCostumeSkin` and `OnCurrentCostumeSkinChanged` reconstruct the outfit | Still changes native hitbox/collider and animations. Owner costume commands or reloads can replace it. Not a guarantee of cosmetic-only behavior. |
| Change the equipped weapon's appearance by equipping a stock weapon | **Yes** | Native equipment spawn/owner state and equip RPC | Also changes the actual weapon/moveset. Weapon entity ID is not an independent cosmetic override. |
| Arbitrary player/weapon tint, shader, texture, animation assets | **No general host-only route established** | No corresponding appearance payload found in the inspected actor/player/weapon types | Host renderer or asset edits stay local. Existing skin IDs can select packaged variants; they cannot distribute new assets. |
| Runtime name formatting | **Partly; already used for the host's owned name** | Native `playerNameSource` SyncVar | Renderer rich-text support and refresh timing vary. Steam/EOS lobby names remain separate. Do not extend owned name publication to guests without auditing their authority path. |

### Scale evidence and the important distinction

`NetworkServer.SendSpawnMessage` writes `identity.transform.localScale` into
`SpawnMessage`. `NetworkClient.ApplySpawnPayload` applies it, including on the
owning client. This is separate from `NetworkTransformReliable.OnSerialize`.

The shipped player prefab in `sharedassets0.assets` has:

- GameObject `74318`, PlayerAvatar `196162`, root Transform `117013`.
- NetworkIdentity `193761`, asset ID `2902183873`, `serverOnly=false`.
- NetworkTransformReliable `194801`, targeting the root, `syncDirection=1`
  (ClientToServer), `syncPosition=1`, `syncRotation=0`, **`syncScale=0`**,
  `coordinateSpace=0` (Local), `interpolateScale=0`.
- Root CircleCollider2D `136064`, radius `0.25`; descendants include rendering
  and hitboxes. A root-scale change is therefore not purely cosmetic.

With this configuration, movement updates neither transmit nor overwrite scale.
Turning `syncScale` on only on the host is **not compatible**: it changes the
serialization layout while stock guests still read the old layout. Transform
authority is also client-to-server; changing the flag does not provide a safe
host-controlled live-scale feature.

Native facing preserves the magnitude of root X scale; `Charm_KillMaxHP` also
changes root scale. A size feature must compose with native changes and provide
consistent application/restoration to every observer. For now, pre-spawn size
is a candidate for a separately tested feature; arbitrary live player resizing
is not a proven capability. Registered enemies may use the same spawn-envelope
principle, but their prefab configuration and AI/collision assumptions need their
own audit.

## FX, sounds, text and markers

These are explicit presentation sends unless the row says synchronized state.
Calling a host-local `SpriteFx.Pool.Spawn` does not broadcast it.

| Capability | Existing stock receiver / payload | Guest visibility and useful controls | Boundaries |
| --- | --- | --- | --- |
| Impact/explosion-style visuals | `DungeonManager.BroadcastDestroyBullet` → `RpcBulletDestroyed` | Stock bullet key, world position, height, angle, range scale, owner/transparency | Selects the guest's destruction presentation without requiring the original gameplay projectile. Base module can play FX/sound and instantiate configured fragments. No arbitrary color/lifetime field; audit fragments, animation events and derived modules individually. |
| Hit FX and impact sound | `BroadcastAttackBullet` → `RpcAttackBullet` | Stock bullet key, position, angle and body-material parameter | Reads the guest prefab's hit FX/audio; receiver does not attack. No arbitrary sound file, color or scale. Useful new candidate for an audited effect catalog. |
| Healing presentation | `RpcBloodFestivalHealFx`; potion `RpcCreateDrinkVisual` | Green effect at the avatar, or the stock potion's cosmetic presentation at a position | Blood Festival route is silent; audited HP-potion visual includes sound. Actual healing is a separate host operation. Existing Rabbit adapter; user has reported healing audio working. |
| Revive pillar | `UnitAvatar.RpcCreateAdditionalLifeFx` | White pillar at the avatar | Also invokes native screen flash and animation sound. Existing adapter sends only after successful automatic revival. These outputs are not individually selectable in the payload. |
| Shield break, guard and parry cues | `RpcCreateSuperArmorBreakFx`, `RpcGuardFx`, `RpcPerfectGuardFx`, `RpcCreateParryFx` | Fixed native cues; several accept position/angle | Guard variants include audio/camera shake; parry adds cyan PARRY text and uses avatar position for its sprite. Not interchangeable pure particle APIs. |
| Positional objective marker / ping | `PlayerSpawner.CreateMultiplayPing` → `RpcCreateMultiplayPing(Vector2)` | Native player-index marker at a chosen position, clamped to viewport edge | Replaces that spawner's previous ping. Default lifetime 3s, optional startup sound; sprite, size, lifetime and sound are client prefab settings. Public helper has an owner-only cooldown reset; see below. New candidate, not implemented here. |
| Attached persistent role marker | Registered `CharacterDebuff_ChimDamage` carrier with target SyncVar | Fixed green X follows the target on stock peers | Existing Zombie adapter owns a cosmetic carrier and bypasses its server gameplay. Ordinary native startup/expiry can apply status/damage; never spawn an unmodified debuff just for looks. No arbitrary recolor/loop selection. |
| Floating world text without chat history | `UnitAvatar.RpcShowDamageParticle` | Text, position, color and native size selector; supported TMP rich-text markup | Existing prototype; drifts/fades and does not follow a moving character. Unreliable event, no replay or removal. Nonzero native size selector forces red; font glyphs/styles remain client-defined. |
| Ordinary or private chat / overhead bubble | Native DungeonManager chat receiver | Stock bubbles and localized text; existing adapters can target recipients | The stock receiver also writes the chat log. Private delivery does not make it bubble-only. Empty messages do not provide reliable bubble clearing. |
| Existing animation playback | `WeaponControllerSimple.SendAnimationPlayInfo` → `RpcPlayAnimation(string)` | Selects a clip already present on the weapon controller | Transient; this receiver plays on non-server clients, so host presentation needs its native local path too. Animation events/transitions can have behavior. No arbitrary clips or general playback-speed payload. |
| Parameterized screen flash | `UnitAI_MadArmadillo.RpcScreenEffect(Color,float)` | Color and duration are payload fields | Requires the matching spawned native boss component. Anchor acquisition and UI behavior need further audit; not a general-ready camera/flash API. |

The ping helper sets `isPingCreated`, whose reset runs only under
`isClient && isOwned`. Calling it on a remote player's spawner from the host can
leave the host's gate set after one call. Repeated host-controlled objective pings
should audit the native RPC sender directly and provide their own bounded
admission, rather than assuming the public helper supports that use.

The existing red kill effect uses native bullet-destruction presentation.
The kill-audio investigation found a separate native sound-bearing effect path,
but a later routing correction has **not been confirmed by live guest listening**.
Do not infer audible delivery from a queued RPC or from the visible red effect.
See [remote audio findings](remote-event-audio.md),
[death FX](friendly-fire-death-visuals.md),
[healing FX](rabbit-shared-healing-visuals.md), and
[revive FX](respawn-visuals.md).

### Reusing an effect versus replacing an effect

The host can select another **registered stock key** where the message already
carries a key. The guest resolves that key against its own installed assets.
An adapter could replace one audited host-emitted presentation event with
another without altering damage. It cannot automatically replace every visual
emitted by a client's weapon, animation event, predicted attack, or local script.
An added event can overlap the original unless the original sender is controllable.

There is no general arbitrary FMOD event/file transport established by this
audit. The hit/destruction routes select sound through a stock bullet prefab.
Likewise, changing that prefab's particle rate, texture, sound reference, material,
or animation on the host does not send those changes to guests.

`SkillController.RpcCreateCastFx` accepts registered SpriteFx names, but it also
changes casting/charging handles and pooled material/transparency. It was
rejected as a general marker transport. `RpcCreateFlameGround` is not cosmetic:
its receiver adds flame-area records used by gameplay. Methods named `Rpc...`
are not necessarily network methods: the inspected summon-circle `RpcCircle`
is just a local coroutine. Verify registration and receiver body, not the name.

## World state and gameplay

| Possibility | Guest result | Host-only mechanism | Limits / useful extensions |
| --- | --- | --- | --- |
| Rain or snow on supported floors | Native weather presentation | `DungeonManager.ServerSetWeatherStateInSession` writes the `currentWeatherInSession` SyncVar; hook calls `GameCamera.UpdateWeather` | Only renders when the current floor supports it (`multiplySunlight`). Floor render reapplies session weather; default floor rain can remain even with None. Candidate for themed arenas/events, not arbitrary global weather. |
| Stats, capacity, resources and future choices | Native synchronized values and subsequent native outcomes | Existing stat dictionaries, inventory/resource fields and generation consumers | Use shared reconciliation and baseline ownership. Native panels/caches may not refresh immediately; do not regrant spent resources or regenerate old offers accidentally. |
| Stock item/Slate/potion grants and loadouts | Native item instances in guest inventories | Stock IDs and native inventory mutation/sync | Item definitions, rarity, icons, names, descriptions and scripts are loaded locally. A new item ID is not delivered by granting it. Existing grant/retraction ownership is important. |
| Damage, healing, defensive rules, XP and PvP modes | Native HP/MP, status, death, XP and revive outcomes | Host admission/calculation plus existing native state/events | Already the basis for shared PvP, Rabbit, XP and resource features. Host policy need not exist on guests; client prediction, UI checks and individual item consumers still require auditing. |
| Teams, targeting and companions | Host-controlled hostility, AI decisions and damage | Existing shared participant/team predicate and native outcomes | Native client-facing factions are not a universal substitute for the PvP predicate. Audit targeting, homing, effects and damage separately; some presentation remains client-local. |
| Custom encounters using stock enemies/merchants | Registered native entities and their replicated state | Native spawn/despawn and supported AI/stat fields | Supports encounter scheduling, native variants and host rules. New prefab/script registration on the host does not register it on guests. Extra entities and event frequency still cost guest CPU/rendering/bandwidth. |
| Teleports, revives and mode resets | Native movement/life state | Owner-aware native requests, target RPCs and authoritative life state | Existing Teleport All uses `CombatBehaviour.ReqSetPosition` and owner `TargetSetPosition`. A host transform write alone is not equivalent; owners can drive normal movement. Same-floor/readiness/connection checks remain. |
| Faster cutscenes | Only scene-specific possibilities established | Host can advance some graph steps | Guest coroutines, camera and animation waits run locally. Host `timeScale` does not provide synchronized 3–5x playback. No general speed payload was found. |

Weather has a second, different path: `ServerSetRainState` emits a transient
`RpcSetRainState` that toggles rain and flashes the screen. It does not provide
the same retained weather state. Treat it as an event with extra presentation,
not a silent persistent weather setter.

See [native item-definition limits](healing-item-investigation.md),
[teleport authority](teleport-all.md), [XP admission](experience.md),
[merchant variants](merchant-variants.md), and
[cutscene timing](cutscene-speed-investigation.md).

## Recommended opportunities

1. **An audited stock FX catalog:** named semantic events for heal, hit, kill,
   explosion, shield break and revival. Record supported position/scale/audio
   parameters and receiver side effects per entry. Reuse existing adapters;
   avoid creating fake gameplay projectiles just to obtain an effect.
2. **Objective and role presentation:** native pings for rally/objective points,
   current green-X carrier for infected roles, floating text for brief countdowns.
   These have different lifetime and audience semantics; none is a general
   custom HUD replacement. Prefer a retained carrier over repeated per-frame FX.
3. **Stock-content events and game modes:** scheduled enemy waves, survival/escort
   rules, native loadouts, limited resources, team conditions and role bonuses.
   These are combinations of established host decisions and native outcomes;
   each new asset/behavior still needs its own contract check.
4. **Arena atmosphere and native skin variants:** weather on supported floors and
   existing matching skins. Keep skin changes opt-in to a feature design that
   accounts for collision/equipment consequences and restoration.
5. **A separate size experiment:** establish pre-spawn player scaling first and
   measure collision/movement effects. Live resizing is a different, riskier
   problem; do not enable scale serialization or replay spawn messages casually.

## Extension and performance rules

- Keep desired persistent state separate from one-shot events. Native SyncVars
  do not guarantee that every client subsystem consumes a change immediately.
  RPC success does not mean a retained state change or a guest acknowledgment.
- Use existing connection/avatar/run lifetimes. Rejoining is a new connection;
  stale IDs, observer sets and cosmetic carriers cannot be reused blindly.
  Reconcile persistent state on readiness, replacement, native reset and floor
  transitions. Do not replay old heals, inventory grants or explosion events.
- Validate native sender, registration, payload, owner inclusion, receiver and
  referenced assets. A custom NetworkBehaviour or changed serialization layout
  cannot be introduced on the host alone.
- Cosmetic failure must not repeat a gameplay action. Track presentation once
  per admitted event and avoid blind retries after an ambiguous send.
- Give persistent carriers explicit ownership and native destruction cleanup.
  Suppress only owned gameplay callbacks; never globally disable a real status
  to obtain its visual, and never clear arbitrary client-expected entities.
- Bound emission rate as well as active count. A few rapidly recreated effects
  can cause audio, rendering, pooling and network spikes; different guests have
  different costs. Use event/change-driven sends, no world-wide per-frame scans.
  See the [effect performance audit](projectile-performance-audit.md).
- Test host, stock owner and another stock observer; include late join, reconnect,
  costume change, death/revive, another run and unload/restoration. Inspect actual
  rendering and audio: party transparency, settings, attenuation and pooling can
  alter the presentation despite correct transport.

## Evidence and verification scope

Assembly: installed `Sephiria_Data/Managed/Assembly-CSharp.dll`.

```text
SHA-256 c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510
MVID    ccf97024-28bd-48f1-9666-c768c2bd4b2b
```

Read-only evidence includes selected managed member/CIL inspection, matching
decompiled method bodies, Mirror spawn/transform implementation, and serialized
player prefab fields. Native assets/decompiled source remain outside Git.
Useful retained REA evidence groups:

- Player/appearance: `ev_62c43b93070f254413ceacca3e32d775e9bced9d6740f7e3f256cc666f3997e9`.
- Mirror spawn: `ev_dc55117646a60a8d775072e7fd80a3d02044646ab9c91bc03d08dc650c53ed62`.
- FX members: `ev_bbf45dd08ca1de6f1d126e7ca92f11f853997c7337b1af95cdb0dee2c81313e5`.
- Weather: `ev_75ba6fcc2cee049a62721ff6471dbdfef309ff3a54c4ebb0e4487159ab091f60`.
- Inventory/XP/movement: `ev_2ccdae0534ebd48f52374c1060c952a58393b668a3c342352bbd165e24f3b702`.

Selected managed coverage was complete; some metadata user-string terminal flags
were malformed and readable strings were retained. This does not substitute for
Unity gameplay testing. Existing implementations and earlier user observations
are identified separately above; new candidates remain unimplemented/unverified
in a live host/guest session. Research-only changes require documentation/link
checks, not a claim that a new runtime feature has passed tests.
