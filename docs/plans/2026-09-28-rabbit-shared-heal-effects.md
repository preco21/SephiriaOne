# Rabbit shared-heal effects implementation plan

**Goal:** Display the native green HP-potion particles once on each player whose
HP actually increases through Wing-Eared Rabbit's existing shared healing.

**Architecture:** Capture HP and recipient identity at the existing `Share`
boundary, keep `HealPercent` unchanged, and revalidate source and recipient after
its callbacks. A separate optional visual adapter validates/caches the native
zero-argument `UnitAvatar.RpcBloodFestivalHealFx` sender/receiver and invokes it
without new packets, state, assets, polling or delayed replay. Failed compatibility
or visual calls disable only presentation for that addon lifetime.

**Tech stack:** C# netstandard2.1, existing Harmony IL inspection, native Mirror
RPC, linked-source net10 fixture tests and installed-game contract checks.

Constraints: host-only addon; use the current sharing toggle; preserve MP,
infinite HP potions, Survival suppression and normal/native healing. No new
settings, hotkeys or deployment. Every dotnet command uses `-p:DeployMod=false`.
The user approved implementation of the prior investigation in
`docs/rabbit-shared-healing-visuals.md`.

## 1. Reproduce missing recipient effects

- [x] Extend `tests/SephiriaOne.RabbitPotionHookTests/Stubs.cs` with the native
  `hp` field, zero-payload heal RPC and a broadcast recorder. Preserve the alert
  test's rejection of unexpected broadcasts.
- [x] Add tests in `SharedHealVisualTests.cs` using the real production hooks:
  a nearby player gains HP once, receives one visual, and receives no potion or
  Survival callback; full HP/zero gain/death/out-of-range/MP rejection receive none.
  Example success assertions: `recipient.hp > before`, `recipient.Heals.Count == 1`,
  `recipient.HealVisuals == 1`, `recipient.DrinkEvents == 0`.
- [x] Run `dotnet run --project tests/SephiriaOne.RabbitPotionHookTests -c Release
  -p:DeployMod=false`; expect the new successful-recipient visual assertion to fail.

## 2. Implement optional visual boundary

- [x] In `RabbitPotionNativeHooks.Share`, capture `recipient.hp` and identity,
  call `HealPercent` once, then check finite increased HP, current source sharing
  context, unchanged live connection/spawner/avatar/floor, readiness and life.
  Emit once only for a surviving valid recipient. Keep recipient failures isolated.
- [x] Add `RabbitPotionNativeVisuals.cs`: validate sender's registered native RPC
  name/hash, empty payload, reliable channel/owner inclusion, empty reader and
  visual-only body. Cache an open delegate to the wrapper. Reset cache on hook
  unload. Warn once and skip visuals after failed validation/send; healing continues.
- [x] Exercise invalidation during healing: source or recipient death, run/dungeon/
  floor/connection/avatar changes, removed membership and changed sharing option.
  Verify FX failure keeps HP, later recipients and infinite/MP/Survival semantics;
  reconnect/new-run recipients get only their new heal event, never old replay.

## 3. Verify native transport and finish

- [x] Extend `GameRabbitCompatibilityTests` to validate actual native RPC, receiver
  and registration and reject changed sender/payload/receiver instructions. Keep
  HP-only and existing gameplay contract checks.
- [x] Build Debug/Release with deployment disabled; run potion and description
  suites, runtime integration, pure/installed-game contracts and relevant catalog
  tests. Review with a read-only reviewer and fix concrete findings.
- [x] Update investigation, feature/development docs and version. Commit and
  push follow using the repository's Conventional Commit workflow. State that live
  particle placement and multiplayer rendering remain unverified.
