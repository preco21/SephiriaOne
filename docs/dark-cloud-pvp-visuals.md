# Dark Cloud PvP visuals: host-only feasibility

Investigated 2026-10-24 against installed Sephiria 1.0.33 and addon 0.94.0
(`8f0c51d`). **Research only:** no gameplay changes, deployment or live multiplayer
test. The user reports damage without the corresponding visual on hostile players.

## Confirmed cause

The player branch of `ComboEffect_DarkCloud.FireLightning(UnitAvatar, int)` does:

1. Calculate the strike position from the target and apply damage on the host.
2. Invoke the native attack callbacks, including Blazing Stormcloud's Burn.
3. Call `TargetCreateFx(connectionToClient, position, empowered, isIce)`.

That connection belongs to the cloud, normally the attacking player. It is not
the victim and is not a broadcast. Thus the owner receives the visual while the
victim/other observers do not. The addon extended targeting and damage to hostile
players but retained this native owner-only presentation route. This explains the
reported asymmetry without requiring a client particle-performance fault.

The existing fixture's `Effects` counter verifies that a strike reaches the
native visual call, **not that guests receive or render it**. Prior descriptions
that admitted strikes preserve native FX should be read with that limitation.

## Exact original FX can be reused

The existing stock `TargetCreateFx` message contains only:

- `Vector3` target world position;
- `bool` empowered/EX variant;
- `bool` ice variant.

Its generated sender uses channel 0 and function hash `-1744830929` (the native
Mirror message stores the ushort form). Its stock receiver has no owner check:
it reads those three fields and invokes the corresponding cosmetic body. The
explicit network connection is not serialized; the receiver passes null for it.

The body uses `SpriteFx.Pool` to construct the beam, target impact and cloud-side
origin effect, then calls `DarkCloudObject.Attack()` for animation and native
attack sound. It does not damage, spawn a gameplay projectile, apply a debuff,
spend cloud charges or alter ownership. Supported native keys are:

- `LightningFx`, `LightningHeadFx`, `LightningTailFx`, and their `_EX` variants;
- `IceLightningFx`, `IceLightningHeadFx`, `IceLightningTailFx`, and their `_EX`
  variants, plus `IceLightningHeadFx_Plus`.

`OnStartClient` creates the `DarkCloudObject` for every observing client before
the owner-only branch. Stock clients already register this RPC. Mirror's native
`SendTargetRPCInternal` accepts an explicit connected client; it does not require
that client to own the sending component. Therefore a host addon can route the
same native message to additional observers without guest scripts or new assets.
This is a static compatibility finding, not evidence of a successful live send.

## Recommended implementation boundary

Forward the **existing visual emission**, only for an admitted hostile-player
strike, to ready, same-floor clients already observing the cloud's network
identity. Keep the native owner send and exclude that connection from forwarding,
so the attacker receives the effect/sound once. Include the host's local client
when it is another observer. Reuse the captured native position and variant flags.

Scope forwarding through the shared friendly-fire participant/mode rules, and
capture strike admission before damage callbacks: a lethal strike still needs
its visual even if a death callback ends a Tournament pair or another mode.
Recheck the cloud, source connection, run/floor and observer identity before
sending; never send to a replacement connection using stale references.

The current Dark Cloud whole-strike limiter runs before damage, callbacks and
visuals. Suppressed repeats must produce no extra FX. Forward at its actual
native visual call rather than running a second damage event or reconstructing
the strike from a timer. Shielded/evaded admitted strikes may keep their native
visual indication, but off/zero-damage policies and excluded same-team targets
should not gain new hostile-hit presentation.

Cache native methods/delegates and validate sender/receiver layout once, not per
hit. Work is proportional to the small observer count only on admitted strikes;
there is no extra Update loop or persistent effect entity. Failed visual sends
must never retry damage or suppress native owner presentation. Avoid retrying an
ambiguous send, which could double its sound. Native pooled FX own expiry.

### Alternatives and limits

| Approach | Assessment |
| --- | --- |
| Forward `TargetCreateFx` to existing observers | Preferred: preserves exact lightning/ice/EX variants, source-to-target beam, animation and sound. |
| Call the existing `RpcCreateFx` broadcast | Cosmetic and stock-compatible, but lacks the ice flag; retaining the original owner send would duplicate its FX/audio. |
| Use an unrelated impact via the existing dungeon cosmetic adapters | Possible fallback architecture, but unnecessary when the exact effect works. A chosen lightning prefab's hit/destroy behavior would need its own asset audit. |
| Spawn `SpriteFx` only on the host | Does not transmit to guests. |
| Change `isPlayerCombo`, authority, faction or invoke NPC delayed lightning | Reject: changes gameplay/targeting/damage rather than just presentation. |

The **area/radius ring** is separate: `DarkCloudObject.SetCloudVisible` explicitly
activates `cloudAreaEffect` only when `visible && isOwned`. That is local client
presentation, not a SyncVar the host can toggle. Forwarding the strike message
does not expose the ring on stock guests. It does expose a beam and impact at the
victim, identifying the source cloud without requiring the ring.

Each peer positions its own cloud visual relative to the owner; beam origin
positions may differ slightly. The exact target world position is in the message.
If the cloud has not spawned or its client visual is unavailable, the receiver
cannot render the beam; observer/readiness gates are necessary. Map/graphics
visibility, actual sound audibility and timing remain live-test questions.

## Evidence and follow-up verification

`Assembly-CSharp.dll` SHA256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.

- REA `ev_70dfaf0ff50fde0d8062c91b16991a1b957a90d0d6f6b46571b9ef77341bf339`:
  selected `ComboEffect_DarkCloud` methods, callers, serialization and receiver.
  `FireLightning` token `0x06004d85`; target sender `0x06004d86`; target body
  `0x06004da1`; receiver `0x06004da2`; client startup `0x06004d78`.
- REA `ev_77d8f169472e843cf9e36b23156fa678be38300bb6f4dbe89b6e9e2c2062c95c`:
  `DarkCloudObject`/`ComboEffectBase`; visibility, client-owned ring, animation/audio.
- REA `ev_c092dde24bbd16d21ac2524ced3d8d06bcd3d09e6e52ccea52f6815dff35550b`:
  `Mirror.NetworkBehaviour.SendTargetRPCInternal`, explicit-connection delivery.
  Mirror SHA256 `c391c91ba860c537692c185b1075f4219ec83fa71a11990a2b51c13876ab7440`.

Selected member coverage was complete. REA retained readable user strings with a
metadata terminal-flag warning; no target code was executed. Focused ILSpy source
corroborated control flow. Temporary exports stay outside Git under
`%TEMP%/sephiria-dark-cloud-fx-audit`.

`GameDarkCloudVisualTests` checks the installed owner-only sender, serialized
fields, stock receiver/registration, cosmetic calls, all variant keys, observer
creation and ring/audio distinction. It does not implement forwarding.

A later implementation must test host→guest, guest→host, guest→guest and a third
observer; normal/ice/EX strikes; lethal hits; cadence rejection; each PvP mode;
same-team/excluded players; floor changes, reconnect and addon unload. Verify
one visual/sound per admitted strike without extra HP/Burn/cloud-charge changes.
