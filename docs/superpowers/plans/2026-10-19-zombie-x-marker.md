# Zombie X marker implementation plan

> **For agentic workers:** Use the executing-plans workflow inline. The user
> approved the green X proposal in `docs/zombie-role-visual.md` and authorized
> continuing in the current repository with automatic commit/push.

**Goal:** Replace subtle Poison particles with the native looping green X for
current Zombies, including host and stock guests, without gameplay side effects.

**Architecture:** Keep `ZombieRoleVisuals`' exact receipt/match ownership,
disabled server component, native client following, and network destruction.
Switch the validated registered prefab to `CharacterDebuff_ChimDamage` and
prevent owned carriers from entering native gameplay destruction. Validate the
fixed sprite loop and its silent animation only when creating a carrier.

**Tech stack:** C#, Unity, Mirror, Harmony; executable fixtures and installed DLL
contracts. No new assets or dependencies.

## Constraints

- One cosmetic carrier per current Zombie; no chat/name/countdown changes.
- No real debuff application, timer, damage, sound, stat change or HUD icon.
- Native Chim and Poison remain untouched; do not modify shared prefabs.
- No per-frame validation/reflection/allocations or position packets.
- Preserve stop, respawn, loading, reconnect, reentrant spawn and unload cleanup.
- Every build uses `-c Release -p:DeployMod=false`; no deployment or game launch.

## Task 1: Replace and verify the cosmetic carrier

Files: `SephiriaOne/Features/Combat/ZombieSurvival/ZombieRoleVisuals.cs`,
`ZombieVisualNative.cs`, `tests/SephiriaOne.ZombieVisualTests/{Program,Fixtures}.cs`,
`tests/SephiriaOne.Tests/Compatibility/GameZombieVisualTests.cs`, version metadata,
`docs/zombie-role-visual.md`, and `docs/README.md`.

Interfaces remain `Update(PlayerAvatar)`, `Clear()`, `Shutdown()` and
`ValidateAndGetPrefab()` (return type becomes `CharacterDebuff_ChimDamage`).

- [x] Add a regression invoking native `Destroy()` on an owned visual. It must
  stay alive without gameplay-end callbacks, while ordinary debuffs still end.
  Run the dedicated visual suite and observe the new assertion fail.
- [x] Change the native contract test to require asset ID `4098245047` and
  Chim's inherited client-only lifecycle; assert the adapter never calls
  gameplay `Destroy`/`RequestEnd`/`DestroyInner` and verify Chim expiry really
  calls damage so the safety boundary is meaningful.
- [x] Switch the marker type and prefab (`CHIM_DAMAGE`, `BuffFx_Chim`), replace
  Poison-specific zero fields with clearing the clone's unused damage FX/sound
  and bonus ID, and guard owned `CharacterDebuff.Destroy` using the same owned
  set as startup. Keep `NetworkServer.Destroy` for cleanup.
- [x] Require the exact three loop components, enabled sprite/animator, valid
  animator binding, one repeated IDLE state at 10 fps, ten expected sprite
  keyframes and empty animation actions/sounds/transform changes. Reject altered
  or unregistered prefabs. Keep all checks at carrier creation.
- [x] Adapt lifecycle fixtures to native Chim; cover ordinary Chim expiry,
  owned destruction attempts during spawn/unload, real Poison, and cleanup
  without damage/audio. Preserve the 10,000-update zero-allocation check.
- [x] Run `dotnet run --project tests/SephiriaOne.ZombieVisualTests -c Release
  -p:DeployMod=false`, Release build, runtime integration and installed-native
  contracts. Expected: all pass; no build warnings/errors; no deployment.
- [x] Review the focused diff with a fresh reviewer, address actionable issues,
  document outcomes and remaining live-rendering limits, update to `0.66.2`,
  then conventional commit and push current main.

## Review focus

The fixture does not render Unity sprites or deliver packets to a live guest.
Check native expiry/destruction separation, exact asset/animation identities,
runtime-only validation cost, and ownership through reentrant cleanup. Do not
infer live visibility from unit tests or claim a unique Zombie graphic: native
Chim can display the same green X.

## Execution record

- The new owned-expiry regression failed before implementation with the expected
  gameplay-end assertion. The native asset-ID regression independently failed
  against the former Poison build.
- After implementation: 36 visual checks passed, including ordinary Chim/Poison,
  reentrant retirement and zero bytes across 10,000 stable observations.
- Release 0.66.2 build: zero warnings/errors. Runtime: 3,163 integration plus
  69 Bat checks passed. Portable/catalog/installed-native checks passed,
  including 33 compiled Harmony API references against HarmonyX 2.9.
- Shipped assets independently matched enabled states, renderer/animator binding,
  and all ten expected frame names with no animation events or sound.
- No deployment or game launch. Live stock-guest readability remains unverified.
- Independent final review found no actionable code issues. The stale older
  README marker summary was corrected; no findings remain deferred.
