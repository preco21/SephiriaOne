# Wing-Eared Rabbit potion options

## Goal and constraints

Add independent, default-off options for unlimited healing-potion uses and nearby
potion healing while wearing the native `HolyRabbit` costume. Keep native input,
animation, drinking events and healing calculations. Gameplay is authoritative on
the host and works with unmodified guests. Never replay healing during settings
reconciliation, joins, reconnects, floor changes or preset loads. No new hotkeys,
items, network messages or assets. Never deploy during development. Commit and
push verified work using Conventional Commits.

Sharing forwards the actual native potion healing percentage (including the
drinker’s potion bonus) to other alive, ready players on the same floor within
5 tiles (distance < 10 world units). Each recipient retains their own native
healing penalties and HP cap. Pending optional user preference may change the
strength calculation. Support stock regeneration potions 0, 1 and 37 only.

Costume descriptions are local UI: append enabled-effect text on the host without
changing the database or implying stock guests receive description changes.

## Shared interfaces

- Immutable `RabbitPotionSettings(bool infinite, bool share)` exposes `Infinite`,
  `Share`, `HasChanges`. Default is both false.
- `SessionPolicy.RabbitPotions` stores intent; `Clear()` resets it.
- `SessionSettings.RabbitPotionsForUse` resolves the current host session before
  reading intent, without running stat reconciliation. `RabbitPotionsForDisplay`
  is a read-only, current-session, host-gated view.
- `RabbitPotionFeature.Available`, `Initialize()`, `Shutdown()` own gameplay hook
  compatibility/lifecycle. `RabbitDescriptionFeature` has the same lifecycle and
  independent availability. Neither adapter edits Entry.cs.
- `/one rabbit infinite on|off`, `/one rabbit share on|off`, `/one rabbit reset`,
  `/one rabbit status` use shared command authority and scope checks.
- Preset v4 adds `rabbit infinite 1` and `rabbit share 1` rows. Accept canonical
  0/1 when reading, reject duplicate/unknown/malformed rows atomically. v1-v3
  remain compatible; only emit v4 when rabbit intent is enabled.

## Task 1: Shared policy, commands, presets and controls (main agent)

- [x] Add failing pure tests for command parsing, independent flags, reset,
  strict v4 validation and old preset compatibility.
- [x] Implement RabbitPotionSettings/RabbitCommand and policy serialization.
- [x] Add runtime tests for command authority, live scope, preset load, reset,
  compatibility failure and reconnect behavior; implement session integration.
- [x] Expose snapshot state and add a Rabbit panel using the existing native UI.
  Enable buttons require compatibility; off/reset remain available.
- [x] Wire lifecycle, update docs/help/version.

## Task 2: Native potion behavior (delegated)

Own `SephiriaOne/Features/Rabbit/RabbitPotionFeature.cs`, any additional
`RabbitPotionNative*.cs`, and a new `tests/SephiriaOne.RabbitPotionHookTests`
fixture project. Do not edit shared policy, Entry.cs, solution or existing tests.

- [x] Inspect native methods and build failing real-Harmony fixture tests first.
- [x] Intercept successful `WieldingPotion.Drink` with scoped context/finalizer.
  Change its out consumption flag only after a successful eligible drink; retain
  potion events and native effect. Never patch `DecreaseItemOnDrink` globally.
- [x] Capture the actual percentage passed by the single native regeneration
  `HealPercent(float)` call, using a validated narrow transpiler/wrapper (or an
  equally precise mechanism). No global interception of unrelated healing.
- [x] Validate live server ownership, initialized/alive avatar, current inventory
  item/instance and quantity, current costume and expected concrete native effect.
  Use live SessionSettings intent. Off/error/incompatible paths remain native.
- [x] Forward healing to eligible current recipients once, using native
  HealPercent with source excluded, same floor, finite positive strength, strict
  radius and deduplication. No per-player cache or polling. Contain callback errors.
- [x] Guard installation against changed signatures/IL and roll back partial
  hooks. Tests cover off, independent flags, costume changes, cancelled/failed
  calls, native events, nested calls, recipient filtering/errors and shutdown.

Native sources: `%TEMP%/sephiria-healing-audit/` and
`%TEMP%/sephiria-mod-research/game-types/`. ILSpy lives at
`%TEMP%/sephiria-mod-research/ilspycmd/tools/net8.0/any/ilspycmd.dll`.
Installed game managed assemblies are in
`C:/Program Files (x86)/Steam/steamapps/common/Sephiria/Sephiria_Data/Managed`.
See `docs/healing-item-investigation.md` for verified methods/assets.

## Task 3: Costume description adapter (delegated)

Own `SephiriaOne/Features/Rabbit/RabbitDescriptionFeature.cs`, any additional
`RabbitDescription*.cs`, and a new `tests/SephiriaOne.RabbitDescriptionTests`
fixture project. Do not edit shared settings, Entry.cs or existing tests.

- [x] Inspect UI_CostumePanel and native description formatters/localization.
- [x] Choose a narrow hook that decorates only HolyRabbit’s rendered description,
  leaves original description intact, and never mutates shared database assets.
- [x] Read RabbitPotionsForDisplay and gameplay compatibility to describe only
  active options. No text on a guest/noncurrent session. Refresh safely on changes
  if UI allows it; never replay OnOpened gameplay actions.
- [x] Add relevant fixture tests before implementation: option combinations,
  other costume, repeated refresh/no duplicates, off/reset/unload, fail-closed
  compatibility. Document that ordinary guests retain native local descriptions.

## Task 4: Review and verification

- [x] Review delegated tasks for requirements and quality, resolve findings.
- [x] Build Debug/Release with `-p:DeployMod=false`; run all existing and new
  console suites and installed-game contracts; run runtime performance checks.
- [x] Broad independent review of the complete diff, fix important findings.
- [x] Update completion records/docs and verify diff.
- Finish with the authorized Conventional Commit and push to main.

Live multiplayer/potion animation UI testing remains a manual smoke test on the
installed game; fixture/IL checks must not be described as live verification.

## Completion record

Implemented in `0.16.0`. The complete feature review approved the integration.
Review fixes isolated Harmony bootstrap behind non-inlined adapters, protected
native description ownership, rejected non-finite distance and revalidated live
session/floor/connection state during potion callbacks. Seven suites (1,915 checks
and scenarios total), Debug/Release builds, installed native contracts and the
zero-allocation five-player synchronization fixture passed. Deployment was disabled
throughout. See `docs/rabbit-potions.md` for commands, limitations and live checks.
