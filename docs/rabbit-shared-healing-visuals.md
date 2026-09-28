# Shared-healing particle investigation

Investigated 2026-09-28 against the installed Sephiria 1.0.33 assembly, SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Implemented in `0.22.0` using the existing shared-healing toggle. The investigation
below records the native contract and asset evidence used by that implementation.

## Finding

**The host can show the ordinary green HP-potion effect around each shared-heal
recipient, with unmodified guests.** The preferred entry point is the existing
public `UnitAvatar.RpcBloodFestivalHealFx()` method on the recipient.

Despite its name, this method only broadcasts a visual effect. Its client receiver
checks the native combat manager and sprite pool, then spawns the configured
effect at that avatar's position. It does not require the Blood Festival setting,
change HP, consume a potion, raise potion/talent events, or play a sound.
The native Blood Festival caller separately heals before invoking this visual.

The installed assets confirm that both paths use the same prefab:

| Native path | Asset reference |
| --- | --- |
| `PotionEffect_Regeneration.regenerationFxPrefab` | `sharedassets0.assets`, GameObject 79294, `HealFx` |
| `CombatManager.bloodFestivalHealFxPrefab` | `level2`, component 20754, external file 5 -> `sharedassets0.assets`, GameObject 79294 |

The prefab uses `Mouse_Healer_Basic_Tier1_Heal_Front_FX*` and
`Mouse_Healer_Basic_Tier1_Heal_Back_FX*` sprites. Extracted front/back frame 07 was
visually inspected and contains the green/white healing sparkles. `SpriteFx`
component 210624 enables `automaticallyDespawnOnExit`; the native pool handles
cleanup when its animation ends. No replacement assets need to be distributed.

## Multiplayer and gameplay separation

- The existing zero-argument ClientRpc is registered in the game's own
  `UnitAvatar` RPC table. Its generated wrapper uses reliable channel 0 and
  includes the owner. Mirror sends it to ready observers of the recipient avatar,
  so the healed player and other clients observing that avatar can see it.
- The packet contains no new entity IDs, addon messages, replicated fields, or
  manually serialized custom payload. Guests execute their existing receiver and
  use their installed prefab.
- Before `0.22.0`, the addon called only `recipient.HealPercent(...)` for shared healing.
  That method changes HP and emits the native HP-change event; it does not request
  the potion's particle effect. This explains the missing explicit shared effect.
- Keep the existing HP-only healing call. Never call `CreateEffect_OnDrink` on a
  recipient to obtain visuals: that would raise potion events and could grant
  Survival's random-stat bonus. The recommended RPC is presentation only.

## Implemented integration

At the existing successful shared-heal boundary in
`SephiriaOne/Features/Rabbit/RabbitPotionNativeHooks.cs`:

1. Retain current ready/live/owned/same-floor/radius checks and capture recipient
   HP plus its connection, run, dungeon and floor identity before healing.
2. Perform the existing native `HealPercent` call once.
3. If HP actually increased and the recipient remains alive in the same current
   connection/session/floor, request `recipient.RpcBloodFestivalHealFx()` once.
   Recheck the captured source context and sharing intent because native HP
   callbacks may change them. An invalidated lifetime must not publish a stale FX.
4. Isolate optional visual failures from healing and later recipients. Do not
   retry healing to recover a failed visual. Validate the native RPC/receiver
   contract independently so a future visual incompatibility leaves HP sharing
   and potion protections intact.

This shows effects only for actual finite HP gains. Full-health players, healing
reduced to zero, dead/out-of-range players, failed or MP-rejected drinks do not
show a misleading heal. It needs no additional toggle, preset fields, polling,
per-player cache, or rejoin replay: the visual is a transient event for each heal.

An alternative is the private `PotionEffect.RpcCreateDrinkVisual(position)`.
It produces the same green prefab, but also plays potion drinking/regeneration
sounds and depends on the short-lived potion's network identity. The public
avatar RPC is the better fit. Calling `CreateDrinkVisual` or spawning `SpriteFx`
locally alone would not deliver the visual to unmodified guests.

The adapter lives in `RabbitPotionNativeVisuals.cs`. It validates the native RPC
name/hash, empty payload, reliable channel, owner inclusion, zero-argument reader
and visual-only receiver calls before caching an open delegate to the game's
public wrapper. There is no new/custom serializer. A failed check or send logs
once and disables particles until addon reload; healing and potion protections
continue. Unload clears the delegate/compatibility flags. No player is retained.

## Verification

The native sender, receiver, asset identity, green sprite frames and pooled sprite
cleanup were inspected. Installed-game tests verify the registered native guest
receiver and reject changed RPC hash/name, channel, owner inclusion, payload,
reader, gameplay calls or prefab reference. The installed IL uses `ldc.i4 0` for
the reliable channel and `Spawn(prefab, position, owner: null)` for the effect;
these were checked against the actual assembly, not inferred from source syntax.

Linked-source tests cover exactly one FX per healed recipient, actual HP gain
versus zero/non-finite gain, no extra healing or Survival events, stale connections,
death/floor/session/identity changes during HP callbacks, failed FX without healing
retry, continued healing of later recipients, duplicate connection entries, no
rejoin/new-run replay, and unchanged behavior when sharing is off. Live checks should include an
unmodified guest as drinker/recipient/observer, multiple nearby recipients, and
repeated heals across reconnect and new runs. Exact placement and animation
appearance in a running game remain unverified.
No deployment was run.

Verification for `0.22.0`: 167 potion lifecycle checks and 12 localized alert
checks pass, along with 757 session/runtime checks, 35 tooltip checks plus 6
language-refresh checks, pure policy/catalog tests and installed-game contracts.
Independent review repeated the potion and installed-game suites with no
actionable findings. Debug and Release builds use deployment disabled.
