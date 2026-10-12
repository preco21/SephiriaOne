# Automatic respawn pillar

Implemented in `0.50.0`, 2026-10-12, against the installed assembly fingerprint
in [domain findings](domain-findings.md).

Follow-up (`0.51.3`, 2026-10-13): a host-log `MissingMethodException` identified
an unavailable HarmonyX string helper in the lazy validator. Compatible IL
comparisons remove that blocker without changing the successful-respawn boundary
or native RPC. See [dependency evidence and regression](harmony-compatibility.md).

## Behavior and shared boundary

Deathmatch and Tournament play the native white pillar after a successful
three-second automatic respawn. The stock network receiver uses that player's
replicated world transform, not the death location, camera or host position.
The native effect includes a brief revive screen flash and sound for observers;
these are part of the stock RPC, not custom guest UI.

`CombatMatchRuntime.Restore` owns this completion point for both modes.
`ReviveAllAction.TryRevive` reports its actual restoration count as well as the
existing success result, distinguishing successful no-ops from a performed
revival. Only a successful restoration of one player can emit the effect. Native
`RpcRevive` completes first, then the caller rechecks the match, scope, connection,
participation and alive state. The adapter also requires positive finite HP and
finite world coordinates. Recovery callbacks that fail, kill the player again,
replace the run or cancel the match cannot produce a false success effect.
Eligibility checks run before the final match checks because pruning a stale
selected connection can itself stop Tournament during revival callbacks.

Manual revival/Revive All, stopped or expired matches' immediate cleanup, old
connection timers and extra-life saves receive no added pillar. A loading player
retains the original countdown deadline and emits the effect only when recovery
can actually complete. Later ticks do not replay it. Existing recovery and
respawn messages remain independent of optional FX delivery failures.

## Native resource and transport evidence

- `resources.assets`, GameObject `19151`: `AdditionalLifeReviveFx`.
- `SpriteFx` `34226`: native pool size five, automatic despawn, no override
  animation transition. No new network object or custom prefab is created.
- Main animator `34703`, animation set `28048` (`ReviveFx`): 11 frames at 20 fps,
  no gameplay frame events, native sound event at frame zero. The main white
  `SpriteRenderer` is sliced to a 1.5625-by-14-unit pillar; a second animator
  supplies the ground ring. Resource frames were inspected outside the repo.
- `UnitAvatar.RpcCreateAdditionalLifeFx()` has an empty reliable payload, native
  hash `-2080231604`, and includes the owner. Stock clients register the receiver.
- `UserCode_RpcCreateAdditionalLifeFx` calls
  `SpriteFx.Pool.Spawn("AdditionalLifeReviveFx", transform.position + Vector3.down * 0.001f)`
  then opens `UI_ReviveEffect`. The 0.001-unit offset is native sorting behavior.
  No party-effect transparency filtering is applied by this path.
- `UI_ReviveEffect` fades the flash and closes itself. The installed `level2`
  `ReviveEffect` object `2163`, component `24720`, has `hasControl = 0` and
  `isPlayerUITHing = 0`; it does not capture input or mark players as preparing.
  Its optional `reviveSound` reference is null; the pillar animation supplies
  the sound event. The stock observer-wide flash cannot be separated from this
  RPC on unmodified clients.

Ordinary `UnitAvatar.Revive` and `RpcRevive` do not already spawn the pillar.
Do not substitute `PlayerAvatar.ReviveAnotherPlayer` or `RpcRevived`: those paths
include HP transfer or player-revival callbacks and owner-only presentation.
The casting-effect transport changes casting/visibility state and is unsuitable.

`Infrastructure/NativeReviveVisuals` validates the exact stock serializer,
receiver, prefab key, world-position source and presentation call sequence once
per addon lifecycle. It resolves the native sender only after validation and
disables only the optional visual if the contract changes. It does not spend an
extra life, modify HP, replay revival or introduce per-frame scans. A visual
exception cannot turn a successful recovery into a failed match.

## Verification limits

Runtime fixtures exercise both modes, deadlines, repeat ticks, manual revivals,
loading delays, invalid HP, callback failures, reconnect/replacement, run changes,
death during recovery, stop/expiry cleanup and optional FX failures. Installed-code
tests verify native payload/registration/world placement and reject changed
contracts. The regression for lazy selection pruning during a Tournament revival
failed before reordering the final guards and passes with the fix.

Debug and Release builds passed with zero warnings/errors. Verification passed:
1,799 runtime integration checks plus 69 Bat checks, 967 combat checks, 27,864
Tournament scheduler checks, and the full portable/installed-game contract suite
against the Release addon. All builds used `-p:DeployMod=false`.

No live Unity render or multiplayer session was run, and no addon was
deployed. Check the pillar and native flash/sound with an unmodified guest,
simultaneous respawns, and a spectator. The native pool can recycle an older
animation if more than five overlap.
