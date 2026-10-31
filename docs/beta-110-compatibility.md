# Sephiria 1.1.0 beta compatibility — 0.107.0

Audited 2026-11-01 against installed Steam app `2436940`, branch
`workshop-beta`, build `26324456`. The addon still loads through the game's
`AddOns` / `HorayModBase` loader. No guest addon or new network protocol is
introduced by this port. Follow the [official developer materials](references.md#official-developer-guide-and-materials).

## Report and causes

The user confirmed that the addon loads but displays compatibility warnings and
PvP does not work. The locally available Player.log was written just before the
beta installation, so it cannot establish the beta's exact runtime exceptions.
Inspection and executable contract tests nevertheless reproduced several native
API/IL changes that disable addon features. The initial independent report had
48 passing and 24 failing groups (some groups include the same nested checks).

- `PlayerSpawner.Initialize` adds `ERestartPlacement`. The old four-argument
  lookup prevents participant, starting-resource and inventory hooks installing.
- Damage defense and Evasion calculations move into native helper methods.
  Elemental-stat script registration adds a local-player predicate.
- Sound playback now passes through game-owned `SoundManager` wrappers, and
  presentation carriers have changed assemblies and a new Blink rotation path.
- Restart overloads and database initialization order change; fountain XP uses
  a request handler rather than its old per-frame grant path.
- Three Ice Weapon emitters add critical-bonus configuration after `swingId`.
  Rejecting a capped spawn at the old boundary leaves invalid IL. The deeper
  standalone projectile suite reproduced this after the main report passed.

See the focused [combat](beta-110-combat.md), [items/resources/XP](beta-110-items.md)
and [presentation](beta-110-presentation.md) audits for evidence and limitations.

## Shared player initialization and lifecycle

`NativePlayerInitialization.ResolveInitialize` resolves the beta's exact
five-argument instance method, retaining the previous four-argument form.
Reflection uses exact binding so an unrelated integer parameter cannot match
the new enum. Wrong return types, static methods and inherited/unknown forms
are rejected. Resolution runs at hook installation, not during hits or frames.

Combat participant retirement/reinitialization, starting grants and inventory
restoration all use this shared boundary. Join initialization and fresh-run
restarts reach the same body. Costume changes still use their existing native
recalculation path. Tests also inspect disconnect, connection replacement,
server stop and run-start ordering rather than assuming a roster slot survives
a reconnect.

The beta's `RestartNewGame(int)` delegates to its placement-aware overload.
The restart coroutine clears network objects/run-scoped rejoin state, calls
`NewGame`, then initializes every remaining player. `FirstStage` placement is
restricted natively to a solo custom-game start; it defers movement until after
initialization and then uses `DungeonManager.LoadStageAndMove`. The existing
departure resource hook still covers that transition. Custom workshop maps and
third-party scripts have not been tested live.

## Ice Weapon projectile denial

The following beta emitters retain their new `FROSTRELICCRITICALDAMAGE`
contribution when admitted, and skip it together with `swingId` when denied:

- Ice Air Slash (`Charm_AirSlash.AirSlashCoroutine`)
- Ice Spear (`Charm_IceSpear.FireSpearCoroutine`)
- Frozen Bow (`Charm_IceBow.FireArrow`)

The limiter validates the exact critical read/add/store sequence before moving
the denial continuation. Iterator waits, retrigger processing and Bow loop
progress remain native. The Hammer already skips the whole configuration through
its throw RPC, so its new critical write needs no production change.

All 22 configured emission bodies were compared with the prior private source
export. Palas' Card, Liberation and Bolt Magic emission bodies are unchanged.
Other differences include native sound wrappers, Bow's negative reload modifier
handling and explosion camera/vibration feedback; those remain native. No new
runtime counting, per-hit reflection, destruction or network traffic is added.

## Verification

- Non-deploying Release build against the installed beta: zero warnings/errors.
- Full ordinary native-contract runner and independent report: 73 report groups
  pass. The ordinary runner also invokes the new audio-wrapper audit.
- All 41 compiled Harmony API references resolve against the co-installed
  HarmonyX 2.9 assembly (metadata check, not execution on Unity Mono).
- Standalone projectile contracts: 519 checks in 64 cases; zero failures.
  Rewritten native IL is stack-verified and JIT-prepared without executing game
  methods. Mutation checks reject changed critical keys/getters/arithmetic/stores;
  synthetic prior-layout bodies retain valid continuations.
- Combat hooks: 15,831 checks; shared runtime/session: 17,897 plus 69 Bat checks.
- Starting hooks: 96; resource budgets: 66; Slate grants: 68; Rabbit artifact: 69;
  potion hooks: 430; merchant: 642. Initialization adapter: seven focused cases.
- Entry lifetime, disconnect, Play Tag, cover, projectile ledger/runtime,
  native effect lifetime, role/impact/audio routing and panel fixtures pass.

The new `--compatibility-report` mode runs native test groups independently so
one rejected optional adapter does not hide unrelated failures. It does not
replace the normal suite or the standalone projectile tests.

Reproduce the main checks from the repository root in PowerShell:

```powershell
$managed = 'C:/Program Files (x86)/Steam/steamapps/common/Sephiria/Sephiria_Data/Managed'
$addon = Join-Path $PWD 'SephiriaOne/bin/Release/netstandard2.1/SephiriaOne.dll'
dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.Tests -c Release -- $managed $addon
dotnet run --project tests/SephiriaOne.Tests -c Release -- --compatibility-report $managed $addon
dotnet run --project tests/SephiriaOne.ProjectileHookTests -c Release -- $managed $addon
```

No deployment was performed. These checks do not run Unity or prove live
host/guest damage, rendering, audio or movement. Previous native layouts remain
supported by adapters/fixtures; a complete old-game binary was unavailable for
a second full compatibility run. New beta builds must be checked again rather
than accepted by version name alone.

## Live verification still needed

After installing the built addon in a separate testing step, verify PvP in the
lobby and a dungeon with an unmodified guest; test blocking/Evasion, potions,
death/respawn FX and audio, and every match mode's cleanup. Enable projectile
limiting and exceed Ice Weapon/Bolt limits, then change floors, restart a run and
reconnect a guest. Confirm saved settings, inventory and Fountain/starting
resources reapply. Capture a fresh beta Player.log if a warning remains.

Do not commit game assemblies, decompiled sources, extracted assets or private
logs. Their temporary local exports are evidence for this audit only.
