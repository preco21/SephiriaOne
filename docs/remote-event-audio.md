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
same reliable channel 0 as the native red FX. Recipients must be current ready
observers of both the victim and dungeon, have matching current player/spawner
identities, and be on the victim's floor. Spectators may hear it too.

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
