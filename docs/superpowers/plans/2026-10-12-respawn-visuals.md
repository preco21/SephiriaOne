# Automatic respawn visuals implementation plan

Goal: show the existing white revive pillar after successful three-second
Deathmatch and Tournament automatic respawns, at the revived avatar's position.

Use the shared `CombatMatchRuntime.Tick` completion path. Add an explicit restored
count to `ReviveAllAction.TryRevive` so a successful no-op cannot emit an effect.
After native revival and revalidation of the current scope/connection/life, call
a separately guarded `Infrastructure/NativeReviveVisuals` adapter. Manual revives
and immediate stop/expiry cleanup keep existing behavior. No guest addon/assets.

Native `UnitAvatar.RpcCreateAdditionalLifeFx` uses the exact white
`AdditionalLifeReviveFx` at the avatar's position, with a 0.001-unit native sorting
offset. It also invokes the native brief revive screen flash/sound. It does not
perform revival, spend an extra life or mutate stats. `PlayerAvatar.RpcRevived`
was rejected because it invokes player-revival callbacks and only creates the
pillar for the owner. Casting FX changes unrelated casting/visibility state.

- [x] Add executable runtime tests for both modes, the three-second deadline,
  moved/replaced avatars, manual revival, failed recovery, expiry/stop and callback
  scope changes. Observe the missing-effect failure before implementation.
- [x] Extend the one-shot revive result and wire the effect only after actual
  recovery. Catch optional presentation errors independently of match recovery.
- [x] Add the native adapter with cached installed-code validation; verify stock
  receiver/registration, world transform, empty reliable payload and presentation
  side effects. Preserve normal native revive RPC ordering.
- [x] Build Debug/Release and run runtime/combat/Tournament/native contract suites
  with `-p:DeployMod=false`. Review, document native evidence and limitations,
  and version. Never deploy or launch the game.

Delivery: conventional commit and push to the configured branch after checks.
