# General development findings

Consolidated 2026-10-10 (Asia/Seoul) from repository research through `0.39.0`.
This is an onboarding guide for agents and contributors, with links to the
detailed evidence. See the [index](README.md), [domain findings](domain-findings.md)
and [reference catalog](references.md).

## Installed SDK and loading

The inspected installation is managed Unity/Mono. `HorayModBase`, `HorayModAPI`,
`AddOnMetadata` and `AddOnLoader` are in `Assembly-CSharp.dll`; this project does
not use a separate HorayModAPI NuGet package or DLL. It targets `netstandard2.1`
and references the installed game/Unity/Mirror assemblies with `Private=false`.
See the [project](../SephiriaOne/SephiriaOne.csproj).

The loader scans immediate child folders of `<GameDir>/AddOns`. A complete
installation is an addon folder containing its DLL and `metadata.json`, with
explicit `modName`, `modVersion`, `modAuthor`, `dllFile` and `entryClass` values.
The entry is `SephiriaOne.Entry`, a concrete `HorayModBase`. Do not rely on the
loader's first-DLL or entry-discovery fallbacks. Renaming a folder inside AddOns
does not disable it. Keep only one active copy.

`HorayModBase` is not a Unity component. `Entry` owns separate controllers when
Unity callbacks are needed. Loading precedes database initialization: subscribe
in `OnModLoaded`, use the appropriate database callback, and resolve cross-database
references when ready. `OnAllDatabasesReady` still precedes some pool setup.
Server and client lifecycle events are separate. Database readiness is not
player/inventory/session readiness.

Unsubscribe hooks and release owned state on unload, but do not assume returning
to title unloads the assembly. A full process restart is needed to activate a new
DLL. The built-in loader does not supply a general dependency order or automatic
`Libs/` loading. The project embeds Harmony and its license. Other addons may load
a different Harmony implementation first; installed API compatibility must be
checked, not inferred from the build package alone. The prior resource-hook
failure involved a Harmony helper absent from the loaded HarmonyX assembly.

Evidence: [loader/API notes](development-notes.md#loader-and-api-findings),
[resource compatibility correction](resource-settings-implementation.md),
[dependency notices](third-party-notices.md).

## Build, inspect and verify without deployment

The default game path is
`C:\Program Files (x86)\Steam\steamapps\common\Sephiria`.
`GameDir` is overridable; managed assemblies are under `Sephiria_Data/Managed`.
The [project](../SephiriaOne/SephiriaOne.csproj) deploys after a normal build, so
always pass `-p:DeployMod=false` for development verification unless deployment
was explicitly requested. Do not run the deployment script as a finishing step.

These PowerShell commands run from the repository root:

```powershell
dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false

$gameManaged = 'C:/Program Files (x86)/Steam/steamapps/common/Sephiria/Sephiria_Data/Managed'
$addonDll = Join-Path $PWD 'SephiriaOne/bin/Release/netstandard2.1/SephiriaOne.dll'
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false -- $gameManaged $addonDll
```

For another installation, supply `-p:GameDir=...` to build/test projects and adjust
`$gameManaged`. The test runners are executable projects using `dotnet run`, not
a single `dotnet test` suite. The main runner targets .NET 10; linked-source
fixtures also resolve the game's Newtonsoft.Json via [test build settings](../tests/Directory.Build.targets).
Feature-specific runners under `tests/` exercise actual hooks against controlled
fixtures. Choose the relevant runners from the feature document and project
files; the commands above are a common starting set, not every regression suite.

Use ILSpy to inspect individual installed types and IL before designing a native
hook. PDBs are not needed for names/signatures/method bodies. Decompiled C# is not
the original source and does not recover comments. Serialized scene/prefab fields
may need separate asset inspection; method signatures alone cannot establish UI
visibility or item definitions. Preserve the exact transpiler shape, branches,
labels and native serialization contract in compatibility tests.

The original research used ILSpy CLI `9.1.0.7988`; the reproducible procedure and
temporary export paths are in [decompilation notes](development-notes.md#decompilation-and-symbol-inspection).
Temporary files are disposable research, never project dependencies. Compile
against the original game assemblies. Keep game DLLs, decompiled source, private
logs and build output out of commits/releases. C# checkout line endings are LF
under [.gitattributes](../.gitattributes).

## Shared feature implementation

Chat and panel actions go through `SettingsActions.Execute`. Parsers/planners live
with the feature; `SessionSettings` owns host policy, scope and coordination.
Do not duplicate native writes in buttons. UI availability is advisory; services
still validate authority, readiness and every participant when executing.

Classify the effect before choosing an adapter:

| Effect | Existing pattern |
| --- | --- |
| One-time change to existing avatars | Shared host preflight and journaled write batch; commit intent after verified readback. |
| Maintained native-baseline relationship | Reconciliation rule with exact observations, typed dependency domains and owned contribution. |
| One-time native event such as drinking/spawning | Read current policy at the native event; do not replay the effect during frame maintenance or re-entry. |
| Early initialization or same-frame native read | A narrow boundary adapter; waiting for LateUpdate may already be too late. |
| Read-only UI/status | Immutable snapshots and presentation notifications, without gameplay writes. |
| Local updater/language preference | Separate local lifecycle/storage; no gameplay preset or player synchronization. |

`StateWriteBatch` supplies preflight, before/target/readback evidence and recovery
journals. Native Unity/Mirror writes are not an atomic distributed transaction.
After a possible partial write, retain evidence and contain further maintenance;
do not silently retry a delta or commit failed intent. Recovery must revalidate
the same objects, run and destructive-write constraints.

Reconciliation distinguishes applied state, verified native fallback, waiting,
suspension, rejection and faults. Only a verified restoration qualifies as fresh
native fallback. Do not acknowledge an incomplete join or stale saved checkpoint
as current merely to stop a warning. See the [extension guide](synchronization-guide.md)
for the concrete APIs and dependency ordering.

Presets store desired settings explicitly through `/one save`; they are separate
from transient run receipts and applied ownership. `/one forget` concerns the
saved preset. Local language/update preferences have their own persistence.
`/one` is the addon-management namespace; `/mod` was deliberately removed.
Existing `/stats`, `/choices`, `/fountain` and `/resources` families remain.

## Evidence, diagnostics and performance

Check both host and unmodified-guest behavior for gameplay effects. Identify the
server/owned-client mutation, native serialization, client definition and native
consumer. A host UI patch or a new host-only item ID does not provide guest code
or assets. See [compatibility scope](presentation-compatibility.md).

Verification has distinct levels: pure arithmetic/policy tests, executable hook
and lifecycle fixtures, installed-game IL/asset contracts, then actual Unity and
multiplayer testing. Do not describe a passed fixture as a live test. Record the
assembly fingerprint and remaining manual cases with each native change.

`/one status` and disconnect diagnostics inspect existing state without processing
joins or writing it. `Player.log` is normally under
`%USERPROFILE%/AppData/LocalLow/TEAMHORAY/Sephiria/`. Transport closure reasons,
write journals and native fallback outcomes are different evidence. A peer close
does not prove a voluntary quit; a post-disconnect warning does not establish
the initiating failure. Collect guest evidence before attributing a disconnect.

Keep exact observations, skip unchanged writes, and use event-driven hooks for
one-time effects. Avoid per-frame scene scans, repeated dictionary/list copies,
formatting, boxing and logging in unchanged paths. Use bounded or object-lifetime
caches only when their invalidation is known. Optimization must preserve native
RNG order, early-read guards, cleanup, readback and rollback constraints.
The [performance reviews](performance-review.md) include allocation fixtures;
their .NET measurements are not Unity FPS measurements.

The settings panel requests explicit `SnapshotContent` sections through the
shared read-only capture path. New tabs should select the sections they display;
omitted player dictionaries are immutable empties, not cached previous values.
Identity/readiness/permissions/feature settings are always read. Diagnostics imply
all sections, and the original `ReadSnapshot` API still returns complete values.

New user-facing text belongs in both bundled EN/KO JSON catalogs, with preserved
format placeholders. Korean is the default; saved selections override it.
Catalogs are cached and reload explicitly, and partial saved catalogs inherit
new bundled keys. Details are in [localization](localization.md).

The [updater](github-updates.md) demonstrates local background work: network and
package IO run off the Unity thread, results publish on the controller, and
installation requires an explicit command/button. Versioned DLL staging plus
atomic metadata selection avoids replacing the loaded assembly. A restart is
still required; package validation is not a game-compatibility guarantee.

For UI extensions, reuse registered native roots and `UIBase` control ownership.
Opening an addon window directly must not depend on the pause launcher. Native
Options keyboard content uses a layout group and size fitter, so an owned child
can be added and removed without replacing tab arrays or native listeners.
`RebindActionUI` is not a safe general shortcut picker: its conflict handling can
modify other native bindings. Local shortcuts need their own persistence,
read-only conflict checks and capture/focus lifetime. See
[tabbed UI and hotkey findings](tabbed-settings-ui.md).
