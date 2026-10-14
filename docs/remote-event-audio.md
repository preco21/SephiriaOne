# Healing and kill audio for stock guests

Implemented in `0.54.1`, 2026-10-14. Host-only installation, no new option or
preset schema. The host and guests use the same native network delivery paths;
the addon never also plays a local copy. Existing particle/death success and
connection lifetime guards remain in charge of event admission.

## Rabbit healing

Normal HP potion use already calls `PotionEffect.RpcCreateDrinkVisual(position)`
once from `CreateEffect_OnDrink`. The stock client receiver dispatches the virtual
`CreateDrinkVisual` on the current `PotionEffect_Regeneration`: it plays the native
drink sound, then `regenerationSoundEvent` and the green `HealFx`. This reaches
the drinker and observers through reliable channel 0, including the host. Do not
add another sound send for the drinker, or call `CreateEffect_OnDrink` again.

Shared recipients previously used `UnitAvatar.RpcBloodFestivalHealFx`, a strictly
silent particle RPC. They now use the current potion's **cosmetic** drink-visual
RPC at the recipient's position, instead of sending both presentations. The
native potion remains spawned through the completed-drink callback; this send
precedes native wield cleanup. It never drinks a potion on the recipient, alters
inventory/MP, repeats healing or invokes Survival/potion events.

The exact-regeneration-effect and active-network-object checks complement the
existing source/recipient lifetime validation. Only finite, actual shared HP
gain emits presentation. Full-health, dead, rejected, stale or out-of-range
recipients remain silent. Duplicate connection entries remain deduplicated by
avatar. Run/reconnect transitions do not replay transient effects.

`RabbitPotionNativePresentation.cs` validates the native sender, hash
`-1992013641`, Vector3 payload, owner inclusion, registered reader and cosmetic
base/override bodies before caching an open delegate. If unavailable, the old
green-only presentation remains available. If a send **throws after being
attempted**, it is not retried and no fallback FX is emitted for that event,
because the packet may already have been queued. Later heals can use silent FX.
Unload resets optional compatibility state; gameplay stays independent.

## Friendly-fire kills

The existing red indicator (`PlayerDefaultBigBulletDestroyFx`) has no audio.
`PlayerSpawner.HandleDieClientside` plays `dieSoundEvent` only for
`isLocalPlayer`; other clients write a death log. There is no general native
audio RPC for selecting arbitrary FMOD events. The exact `PlayerHit` reference
was found only on the player spawner in the inspected scene/resources assets.

The user approved a native magic-impact fallback for remote observers. The
`FlameBall` prefab's **base** `BulletDestroyModule.CreateDestroyVisualOnClient`
has a sound but no FX prefab, alternate effects or fragments. Using the existing
`DungeonManager.RpcBulletDestroyed` receiver for this prefab plays audio only.
No projectile is spawned, destroyed, exploded or made to deal damage.

`FriendlyFireDeathAudio` independently sends that exact native payload through
Mirror's `SendTargetRPCInternal` on the current dungeon identity. It uses the
same reliable channel 0 as the native red FX. Since `0.54.3`, recipients must be
current ready observers of the dungeon, have matching current player/spawner
identities, and be on the victim's nonempty floor. Spectators may hear it too.
The victim's observer list and full inventory/stat readiness no longer gate
cosmetic delivery; neither is consumed by this receiver.

- Remote victim: host and other observers get one impact cue at the victim's
  world position. The victim retains their one native local death sound.
- Host victim: the host retains native death audio; remote observers get the cue.
- The host's `LocalConnectionToClient` uses the same targeted message delivery;
  no separate `RuntimeManager.PlayOneShot` call can double host playback.

The shared successful-death finalizer dispatches FX and audio independently
before round progression. Repeated `Die` calls on an already-dead player,
extra-life saves, failed deaths, retired receipts and disabled combat cannot
emit again. The existing semantics cover casual FF, Deathmatch and Tournament,
including environmental deaths that already receive the red indicator. One
listener's failed send cannot prevent later listeners receiving the cue, and
uncertain sends are never retried. Effects, accounting and respawn continue if
this optional adapter fails. No per-frame work or persistent event cache exists.

## Asset evidence and validation

Inspected the installed game assembly documented in [domain findings](domain-findings.md),
`sharedassets0.assets`, `resources.assets`, `level2`, and FMOD banks. Asset scripts,
decompiled source and extracted metadata remain outside the repo. FMOD metadata
was queried with output disabled; the game was not launched.

| Event | Native path | One-shot / 3D | Length |
| --- | --- | --- | --- |
| Healing | `event:/Scene/healPotion_Small02` | yes / yes | 1,203 ms |
| Remote death fallback | `event:/Scene/hitMagicFire` | yes / yes | 987 ms |
| Victim's native death | `event:/Scene/PlayerHit` | yes / yes | 3,937 ms |

`FlameBall`: GameObject 58255, base destroy module 186594 in sharedassets0.
Its sound GUID fields are `(-33958705, 1201911055, 338028947, 494537983)`.
Runtime validation requires this event, the exact base module and absent visual/
fragment assets. Changed assets disable optional audio. Missing pools during
loading skip the event without retaining it for replay. Native 3D attenuation
and each client's FX volume remain effective; this does not force global audio.

The death adapter reuses the existing audited bullet-presentation transport and
also verifies the installed local-player death-audio gate before excluding the
victim from additional sends. No custom guest protocol is registered.

## Verification and remaining live checks

- Native contract tests cover potion registration, payload, owner inclusion,
  reader and visual-only bodies, source's one send, local-only death audio,
  mutated contracts, and alternate co-installed Harmony API availability.
- Executable tests cover host/remote victims and listeners, payload world
  position, duplicate/stale observers, readiness/floor/ownership checks, asset
  changes, writer cleanup, per-listener failures and no ambiguous-send retry.
- Existing Rabbit tests now cover audiovisual success, no duplicate green FX,
  rejected/no-gain/stale recipients, callback changes and silent fallback.
  Death lifecycle tests cover single audio dispatch alongside FX, duplicate death
  suppression, invalidated callbacks and audio surviving optional FX failure.
- Build/test commands retain `-p:DeployMod=false`. No deployment or game launch.

Live listening remains necessary: test host and stock guests as drinker,
recipient, victim and observer in lobby/dungeon and both match modes; repeat
healing/respawns and reconnect. Confirm perceived volume/attenuation, near-
simultaneous kills and no overlapping copies. Native-code/fixture checks prove
routing and admission logic, not actual remote audibility or timing.

## Kill-audio follow-up (`0.54.3`, 2026-10-15)

The user confirms Rabbit healing audio works but remote-player kill audio is
still silent, while the added red kill FX appears on the other player. This
confirms the shared death presentation and visual broadcast are admitted;
investigate the audio adapter and playback separately. The inspected host log loaded
`0.54.1` and contained no death-audio compatibility warning. The old implementation
silently returned for routine rejection, so that log cannot identify a root cause.
Do not describe the following hardening as a confirmed live fix.

Rechecked the installed and deployed code, resource registration and sound bank:

- `GameDataLoader.Awake` loads `Resources.LoadAll<GameObject>("Bullet")` into
  `ProjectileSpawner<Bullet>`. `globalgamemanagers`' ResourceManager registers
  `bullet/flameball` at sharedassets0 GameObject 58255. Its Bullet 190845 has
  serialized `DestroyModule` 186594. Availability does not depend on first firing
  a FlameBall or the prefab's `Awake` having run.
- Mirror's targeted sender serializes the same `RpcMessage` as the broadcast.
  The dungeon registers this exact receiver; the host connection is present in
  `NetworkServer.connections`, and loopback delivery copies the writer's bytes.
  Targeting a stock ClientRpc does not require a custom guest handler.
- The stock impact receiver resolves the prefab and calls its base presentation.
  Its first branch plays a non-null sound at the supplied world position,
  before checking any FX. `RuntimeManager.PlayOneShot(EventReference, Vector3)`
  forwards to the GUID overload, then creates, positions, starts and releases
  exactly one FMOD instance. A new native contract guard explicitly verifies this
  audio branch; the prior shared visual audit only allowed such a call.
- Rendered `hitMagicFire` through the installed native FMOD library and banks
  with output disabled, explicit sample loading, a colocated listener and master
  DSP metering. It reached PLAYING and nonzero output (peak about 0.29); the
  working heal event also rendered (about 0.059). Both are spatial one-shots with
  3/21 minimum/maximum attenuation distances. This rules out an intrinsically
  silent/missing event in these banks, not in-game mixing, audibility or delivery.

Removed two unnecessary admission dependencies: audio now enumerates the actual
RPC anchor's dungeon observers rather than intersecting victim observers, and
does not require `HostStateAdapter.IsReady` (race/name/inventory/stat readiness)
from a listener. Exact current connection, identity, authority, network readiness
and same-floor checks remain. Sends never retry; the victim retains native local
death audio without an additional impact. No new local-only playback is added.
Tests reproduce both former silent exclusions, but neither was observable in the
reported log, so neither is established as the cause of that session's silence.

The first six adapter invocations per runtime reset write `[SephiriaOne] Death
audio:` diagnostics. Preflight failures name the unavailable stage. Dispatch
summaries include observer counts, `queued`/host counts, exclusions for stale
connections, missing player identities or floors, and diagnostic-only counts for
the old inventory/observer gates. These contain numeric IDs, not player names.
Diagnostic read/log failures are contained and cannot suppress audio; failed
legacy-read comparisons increment `diagnosticErrors` instead of gating sends.
After six entries there is no diagnostic-only readiness work. Compatibility/send
failures retain their separate one-time warning. There is no queue for later
playback, timer, per-frame scan, or guest protocol.

Regression verification: routing/playback-boundary fixtures cover both host and
remote victims, reconnect/stale identities, temporary missing pools, callback
teardown, no duplicate sends, bounded logs and no inventory dependency. Shared
death-finalizer tests confirm one audio dispatch in casual friendly fire,
Deathmatch and Tournament, including repeated `Die` and a new life. Installed
assembly tests check receiver registration, world-position audio arguments,
FMOD start/release, and rejection of removed/moved/inverted audio branches.
The receiver fixture is not an actual guest or FMOD mixer.

Next live verification with the new build, after a full restart:

1. In casual friendly fire, kill a guest near the host. Check the red FX and one
   impact cue on the host and a second guest. The victim should hear only native
   local death audio. Repeat with the host as victim.
2. Repeat after respawn in Deathmatch and on a Tournament round-ending kill.
   A duplicate callback must not replay sound. Verify Rabbit healing remains as-is.
3. If still silent, inspect the bounded `Death audio:` records and the separate
   `Multiplayer death audio unavailable` warning. Check a first kill after a full
   restart, before the six-entry budget is exhausted. A missing record can mean
   no adapter invocation, a warning-only compatibility/asset failure or exhausted
   diagnostics; it is not proof of missing death admission. `queued=0` names
   audience exclusions; a positive queue count means further live receiver/mixer
   evidence is needed. A queue count never asserts guest receipt or audibility.

No game launch, deployment, or live host/guest listening was performed for this
follow-up. Extracted assets, native libraries and private logs remain outside Git.
