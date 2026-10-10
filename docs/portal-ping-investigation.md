# Portal ping audio investigation

Investigated 2026-10-10 against the installed `Assembly-CSharp.dll`, SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Addon baseline: `0.42.0`. This records research, not an implemented option.

## Requested behavior

Provide Native (default), Once per floor and Muted modes for the sound produced
when a player presses F at the next-floor portal. A mode change clears the
per-floor allowance. Preserve native interactions, notifications, visual pings
and the next-floor selection UI. An allowance shared across callers prevents
different players from each triggering another sound on the same floor.

## Native path

- `DungeonStair.MoveTo(GameObject)` calls
  `PlayerAvatar.RequestWorldMap(Vector3, int, bool, float)` with
  `isQuestBoard: false`.
- The host path in `RequestWorldMap`, and the server receiver
  `UserCode_CmdRequestWorldMap__Vector3__Int32__Boolean__Single`, send
  `RpcWriteWantsToNextLog()` before checking gathering, battle and preparation
  conditions. The RPC has no arguments and no audio-selection flag.
- The receiving `UserCode_RpcWriteWantsToNextLog()` writes the native request log
  and calls `PlayerSpawner.ClientCreateMultiplayPing` for a non-owned requester.
- `ClientCreateMultiplayPing(Vector2)` replaces the previous visual ping,
  instantiates `multiplayerPingPrefab`, and selects the requester's cursor.
- `MultiplayerPing.Start()` records its creation position and calls
  `createSoundEvent.Play()`. The sound emitter and marker are local objects on
  each receiving client. The host does not send a separate sound RPC.
- Once gathering checks pass, `RpcOpenWorldMap(int)` opens
  `UI_NewWorldMapPanel`. Its `moveSoundIdx` configures movement audio; it is not
  a parameter controlling the gathering ping.

Other callers matter: `RequestTogetherMoveAppeal` and its server command also
send the same argument-free notification. Stage movement uses
`RpcBroadcastStageMovePing(bool, int)`, which combines the native request log,
visual ping and requester error messages. Ordinary manual pings reach the same
`ClientCreateMultiplayPing` and `MultiplayerPing.Start` through a different RPC.
Globally disabling `MultiplayerPing` audio would also change manual pings.

## Compatibility boundary

The inspected native portal notification offers no way for the host to tell an
unmodified guest to render its marker/log without playing its sound. Dropping
that RPC would suppress more than audio; changing the F/world-map request would
risk suppressing the interaction itself. Neither meets the requested behavior.

A listener-side addon could gate just the emitter playback while leaving native
marker creation, logging and world-map interactions intact. This would affect
only that listener; stock guests would retain native sound. Such an option is
an exception to the project's existing host-only gameplay-effect scope and
requires the user's decision before implementation. The shared notification's
additional callers also need an explicit scope decision or precise source
identification; do not silently change every multiplayer ping.

## Status and verification limits

Source inspection only. No hook, setting, command, UI control or per-floor state
has been added. No addon deployment or in-game test was performed. The user has
been asked whether to permit a local audio setting or retain the host-only scope.

If proceeding, verify Native passthrough, party-wide caller counting on each
listener, mode-change reset, floor/run/session replacement, unmodified manual
pings, preserved notifications and floor-selection UI, and failure fallback to
native behavior. Do not claim host-wide stock-client suppression from a local
playback hook.
