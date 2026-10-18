# Host-only cutscene acceleration investigation

Investigated 2026-10-18 against installed Sephiria `1.0.33`, addon `0.63.0`.
Research only: no runtime hooks, settings, deployment or game launch.

## Result

The inspected game does **not expose a general native way to make all stock
guests play cutscenes at 3x–5x speed**. Some progression is server-authoritative,
but animation, dialogue, camera and fade playback often use independent client
clocks. Faster host progression can interrupt or overtake guest presentation.
It does not retime that presentation.

Selected server waits and native boss-intro skips are possible narrower avenues.
They require scene-specific handling and multiplayer verification; they cannot
be advertised as synchronized, continuous cutscene fast-forward. No general
speed control was added on the strength of this investigation.

## Evidence and scope

Primary artifact: `Sephiria_Data/Managed/Assembly-CSharp.dll` from the installed
Steam game. Fresh REA metadata/member inspection matched the existing local
ILSpy export used to read coroutine control flow:

- SHA-256: `c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`
- MVID: `ccf97024-28bd-48f1-9666-c768c2bd4b2b`
- REA identity evidence: `ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`
- Core playback/member evidence: `ev_a1f154457d91d5c6147c52dd2b2d0b28240c89627474525f99302948366f41d3`
- Node/event/boss evidence: `ev_00dc3874746982f076a45a90143fcc5b6e542a898f96c10bad20cce2c868a06d`
- Server coroutine evidence: `ev_1a96ea59ab898fe51a9c64f91ca362cb4d00fda1e73401555e44c8cd38212ef0`

The selected member sets reported complete coverage. Evidence IDs are research
receipts, not repository dependencies. Tokens below are valid only for this
artifact identity. Game binaries and decompiled source remain outside the repo.
See [inspection workflow](general-findings.md#build-inspect-and-verify-without-deployment).

This is static evidence, not a live host/guest test. Serialized graph/prefab
composition and every story scene have not been exhaustively audited. The
conclusion is bounded to the inspected native interfaces, not a proof that no
scene-specific workaround can ever exist.

## Network graph: host progression, local presentation

`NetworkCutScenePlayer.StartCutSceneCoroutineServer` (`0x06001af4`, generated
`<StartCutSceneCoroutineServer>d__25.MoveNext`) controls graph order on the host:
announce a node, await its host coroutine, choose the next node, then announce
the previous node's end. Its loop does not wait for guest completion acknowledgments.
`Update` calls the current node's `UpdateEvent(Time.deltaTime)` only on the server.

| Native path | Payload/behavior on stock guests | Consequence |
| --- | --- | --- |
| `RpcPlayCutSceneNodeNonHost` / receiver `0x06001b08` | Graph name and node GUID; looks up the guest's graph and starts its local `DoMicroEvent` coroutine | No speed, duration, start timestamp or elapsed position is transmitted |
| `RpcEndCurrentCutSceneNodeNonHost` / receiver `0x06001b0a` | Stops the guest coroutine and calls its `ForceStopMicroEvent` | Early host completion can truncate a guest node |
| `SkipRequest` / guest receiver `0x06001b10` | Calls the current node's native `RequestSkip` on host and guests | Per-node skip semantics, not a general playback-rate API |
| `RpcPlayCutScene` / receiver `0x06001b0e` | Initial position used to relocate the observer near the scene | No playback clock |
| `SerializeSyncVars` / `DeserializeSyncVars` (`0x06001b13` / `0x06001b14`) | Graph name, play-switch name, playing flag | No current-node phase/time snapshot or rate |

These native RPC senders use channel 0 and include the owner. Their receivers
understand only the existing payloads; adding a speed argument or a new addon
RPC cannot make an unmodified guest consume it. Changing a host graph asset also
does not change the guest's graph resolved from its own installation.

The playing-state hook owns HUD hiding, input blocking, pause restrictions and
camera cleanup. Manually clearing that flag is not a substitute for completing
the graph's gameplay actions. The absence of a phase snapshot also means join/
reconnect recovery needs separate investigation before any accelerated feature;
this audit does not claim a reproduced vanilla late-join bug.

## Why one timing patch is insufficient

| Path | Observed timing/termination behavior |
| --- | --- |
| `CutSceneWaitForSecondsNode.DoMicroEvent` | Uses `WaitForSeconds(waitTime)`; multiplying `UpdateEvent` alone does not shorten it |
| `CutSceneSayNode` and `UI_SpeechBubble` | Local typewriter, fixed waits, a separate timer and optional despawn wait; native `Skip()` sets local typewriter speed to 10, not a 3x–5x scene multiplier |
| `CutSceneActorMoveNode` | Waits for arrival; local player movement differs from server NPC movement, while `ScenarioProp` has its own local movement clock |
| `CutSceneWaitSwitchStateNode` / `CutSceneChoicePhaseNode` | Wait for state/selection, not merely elapsed time |
| `CutSceneSetCameraNode` | Applies its locally stored camera speed multiplier; that field is not in the node-start packet |
| `CutScenePlayer` / `CutSceneClip_Timeline` | Local clip loop instantiates a `PlayableDirector`, calls `Play`, and waits for its state; no replicated playback-rate control in this path |
| `UI_CutSceneConversationEventPanel` | Runs graph updates with local delta time |
| `ScreenFader` / `TimelineSpeechBubble` | Use unscaled time for fades/waits or lifetime |

`GameTimeManager` is a local `MonoBehaviour`, not a network component. The
inspected game source's `Time.timeScale` setters are local 0/1 pause/reset paths;
pause deliberately does not pause multiplayer. No corresponding native
time-scale RPC/SyncVar was found. Setting host time scale to 3–5 would affect
broader host simulation without scaling guest clocks or unscaled presentation.

Forced termination also needs node-specific cleanup: `CutSceneSayNode` despawns
its bubble immediately. `CutSceneActorMoveNode.ForceStopMicroEvent` stops cached
player/avatar motion, but does not include the normal coroutine's AI
`externalCommandReceived` decrement or the separate scenario-prop completion.
This is a reason to avoid arbitrary coroutine cancellation, not evidence of a
newly reproduced native bug.

## Bespoke events and boss intros

`Desert_EndEvent.SyncCutScenePhase` starts `ScenarioCoroutine` locally when the
replicated phase becomes 1. That coroutine contains many fixed waits, dialogue,
room-object changes, camera and sound operations. Server writes of later phases
do not seek the client's coroutine to those phases. It also contains quest
progress and server-side player death operations. Shortening only host waits
would let authoritative actions overtake the guest's remaining local scene.

Boss intros provide a narrower candidate: `BossSpawner.EnterEventCoroutineServerside`
awaits the boss's `EnterAction`, then broadcasts intro completion/battle start.
But clients still run local presentation: `UnitAI_QQQBoss.RpcEnterAction_Zoom`
has no arguments and its receiver hardcodes a two-second camera zoom. Shortening
host waits cannot shorten that guest camera operation. `UnitAI_NewBasic` speech
RPCs do carry a time argument, but `UI_SpeechBubble` uses it as dwell after local
typewriter completion; scaling dwell alone does not scale the entire dialogue.

The existing boss skip is more concrete than acceleration. `BossSpawner.Update`
checks `skippable`, native skip votes and a live intro coroutine before invoking
`SkipEnterAction`, clearing speech, restoring combat flags and broadcasting
`RpcHelloEnd` / `RpcStartBattle`. `UI_BossPanel` and `DungeonManager.SkipVote`
feed that path. A future convenience feature should reuse the complete native
flow and deliberately define vote policy, not fabricate individual guest votes
or jump directly to battle-start RPCs.

Some movement operations carry numeric values through native RPCs, such as
`TopdownRigidbody.SetGravityVelocityAndScale`. They can affect a specific
motion on a remote owner, but do not retime the surrounding camera, FX, waits
and dialogue. They are not a general cinematic-speed transport.

## Feasible follow-up boundary

An allowlisted **shorter intros / fewer pauses** feature could retain native
progression and only shorten verified noninteractive waits or use complete
native skip routines. Unsupported scenes, choices and movement/state waits
would retain native behavior. Exact 3x–5x total playback would not be promised.
Uniform acceleration of client-local timelines and presentation would need
guest-side support or a new suitable native interface, conflicting with the
current host-only requirement.

Before implementing even the narrower feature, verify the selected scenes on a
host and stock guest: intro/camera/dialogue completion, input/HUD restoration,
final actor positions, battle/quest effects exactly once, normal completion and
skip, repeated runs, interruption/disconnect/rejoin, and settings/unload during
a scene. Use per-scene contracts and conservative fallback; do not globally
patch `WaitForSeconds`, `Time.timeScale`, or all coroutine termination.
