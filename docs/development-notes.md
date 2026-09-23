# SephiriaOne development notes

Recorded: 2026-09-23 (Asia/Seoul).

This document records the project history, current implementation, and modding
findings collected during the initial investigation. Installed-game observations
apply to the assembly fingerprint below. Reference repositories and documentation
can change independently of that installed game version.

## Current status

SephiriaOne is a C# addon using Sephiria's built-in HorayMod API. Version `0.2.0`
colors the local player's name blue in the character/stats panel and the existing
overhead nameplate. It also logs loading, database readiness, and unloading.
The first feature is cosmetic; gameplay rules are unchanged.

| Area | Status and evidence |
| --- | --- |
| Project | Visual Studio solution and `netstandard2.1` class library exist. |
| Compiler | .NET SDK `10.0.401` is installed and was used successfully. |
| Release build | `dotnet build SephiriaOne.slnx --configuration Release --nologo` completed with 0 warnings and 0 errors. |
| Debug build | `dotnet build SephiriaOne.slnx --configuration Debug --nologo -p:DeployMod=false` also passed for `0.2.0` with 0 warnings and 0 errors. |
| Automatic deployment | The build invokes `scripts/Deploy-Mod.ps1` after the MSBuild `Build` target. |
| Visual Studio command | Added the `Deploy Mod` launch profile. Its command was verified with evaluated Release properties and matching deployed hashes; Debug path resolution was also checked. The IDE dropdown has not been tested interactively. |
| Missing addon folder | Deployment created `AddOns\SephiriaOne` during verification. |
| Existing addon folder | Running deployment again succeeded. |
| Deployed content | SHA-256 comparisons confirmed the deployed `0.2.0` DLL and metadata match the Release output. No game DLLs were copied into build output. |
| In-game loading | The user confirmed `0.1.0` loaded. `Player.log` also contains `[SephiriaOne] Loaded v0.1.0`, the AddOnLoader success entry, and `[SephiriaOne] All databases ready`. |
| Blue name feature | Implemented in `0.2.0`; in-game visual verification of this new version is pending. See [design and verification steps](blue-player-name.md). |
| Multiplayer testing | Pending. The color override is local to this client and checks player ownership. |

## History

All stages below occurred during the initial 2026-09-23 session.

1. Investigated native AddOns development using the user's Xetsumei GitHub and
   Nexus Mods links. The initial workspace was
   `C:\Users\preco\repos\sephiria-one`, which was empty and not a Git repository.
2. Inspected the installed game, its managed assemblies, existing RaidRaid addon,
   and `Player.log`. Confirmed that the installed game already loads native
   addons. At this stage, .NET runtimes were available but no SDK was detected.
3. Downloaded ILSpy CLI `9.1.0.7988` into a temporary research directory and used
   the existing .NET 8 runtime to inspect selected game types. This did not
   require replacing or modifying game assemblies.
4. Found the official API URL embedded in `HorayModAPI` and read the developer
   reference. Compared its documented interface with the installed assembly.
5. Expanded the research to MiraItemMod, CustomCostumeAddOn,
   SephiriaChatTranslatorAddOn, ArcaneForged, and Mira's Item Mod blog post supplied
   by the user. Also inspected MiraModBase to understand dependency loading.
6. Provided Visual Studio 2026 setup instructions for a minimal logging addon.
   The user created the project and directed subsequent work to
   `C:\Users\preco\repos\SephiriaOne`. This is the active repository; it is a
   different directory from the initial hyphenated workspace.
7. Inspected the user's project, preserving its existing source and configuration
   changes. Confirmed that SDK `10.0.401` was now installed.
8. Added the deployment script and post-build target. Built Release, created the
   addon directory, deployed the DLL and metadata, repeated deployment into the
   existing directory, and compared source/destination hashes.
9. Renamed the local branch from `master` to `main` at the user's request.
10. Recorded the user's automatic commit-and-push workflow in
    [AGENTS.md](../AGENTS.md), using Conventional Commits for completed work.
11. Added a `Deploy Mod` launch profile so deployment can be run from Visual
    Studio's Start dropdown using the selected Debug or Release output.
12. Confirmed the `0.1.0` load and database-ready messages in `Player.log`, matching
    the user's successful in-game check. Inspected the name-label UI types and
    implemented blue local character/stats and existing overhead names for `0.2.0`.

The repository already contained commits `c793844` (Git configuration files) and
`736b305` (initial project files). The scaffold adjustments and deployment work
were uncommitted when the first version of these notes was written. Subsequent
commits are recorded in Git history. The configured remote is
`https://github.com/preco21/sephiria-one.git`.

## Environment and project layout

| Setting | Observed value |
| --- | --- |
| Active repository | `C:\Users\preco\repos\SephiriaOne` |
| IDE | Visual Studio 2026 Community, installed under `C:\Program Files\Microsoft Visual Studio\18\Community` |
| Build SDK | .NET `10.0.401` |
| Mod target framework | `netstandard2.1` |
| Mod assembly / namespace | `SephiriaOne` |
| Mod version / author | `0.2.0` / `preco21` |
| Game version reported by the confirmed load log | `1.0.33` |
| Game directory | `C:\Program Files (x86)\Steam\steamapps\common\Sephiria` |
| Game managed assemblies | `<GameDir>\Sephiria_Data\Managed` |
| Addon directory | `<GameDir>\AddOns\SephiriaOne` |
| Unity version reported by the game log | `6000.3.21f1 (c02631ffc030)` |
| Other installed addon observed | RaidRaid; its metadata reported `3.318.0` during the initial investigation. |
| Player log | `C:\Users\preco\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log` |

The inspected `Assembly-CSharp.dll` has this SHA-256 fingerprint:

```text
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
```

This identifies the inspected binary; it is not a game release number.

```text
SephiriaOne/
  SephiriaOne.slnx
  SephiriaOne/
    SephiriaOne.csproj
    Entry.cs
    LocalPlayerNameColor.cs
    metadata.json
    Properties/
      launchSettings.json
    bin/Release/netstandard2.1/
  scripts/
    Deploy-Mod.ps1
  docs/
    blue-player-name.md
    development-notes.md
```

- [Project configuration](../SephiriaOne/SephiriaOne.csproj) references the installed
  `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, `Mirror.dll`,
  `Unity.TextMeshPro.dll`, and `UnityEngine.UI.dll`. All use
  `<Private>false</Private>`, preventing those game references from being copied
  into build output. `metadata.json` is copied with `PreserveNewest`.
- [Entry point](../SephiriaOne/Entry.cs) subscribes to `OnAllDatabasesReady` in
  `OnModLoaded()` and unsubscribes in `OnModUnloaded()`. It also owns a persistent
  name-color controller, which is disabled and destroyed on unload.
- [Name color controller](../SephiriaOne/LocalPlayerNameColor.cs) colors only
  `UI_StatsPanel.characterNameText` and the owned player's `WorldUserName` blue.
  It preserves alpha and restores original text color settings on disconnect,
  rebinding, or unload. It does not make hidden nameplates visible.
- [Metadata](../SephiriaOne/metadata.json) names `SephiriaOne.dll` and
  `SephiriaOne.Entry` as the assembly and entry class.
- [Visual Studio launch profile](../SephiriaOne/Properties/launchSettings.json)
  exposes `Deploy Mod` in the Start dropdown and passes the active project's
  `TargetPath` and `GameDir` to the deployment script.
- [Deployment script](../scripts/Deploy-Mod.ps1) accepts optional `BinaryPath` and
  `GameDir` parameters. Without `BinaryPath`, it checks the project's
  `bin\Release\netstandard2.1\SephiriaOne.dll`, then the equivalent Debug path.
  Discovery is relative to the script location, independent of the working directory.
  `GameDir` defaults to `C:\Program Files (x86)\Steam\steamapps\common\Sephiria`.
  It requires the binary and adjacent `metadata.json` to exist before creating
  the destination. It derives the addon folder name from the binary filename,
  creates missing directories, and overwrites those two destination files.
  It does not copy PDBs, game assemblies, dependencies, or asset directories.

The .NET SDK runs the compiler. The compiled addon targets .NET Standard 2.1
because it runs inside Unity's managed runtime. These are separate settings.
MiraItemMod, ChatTranslator, and ArcaneForged use the same target framework;
CustomCostumeAddOn uses the older .NET Framework 4.8 project format.

## Building and deploying

Run commands from the active repository root. Close the game before replacing
the addon binary, then restart it to load the new build.

```powershell
dotnet build .\SephiriaOne.slnx --configuration Release
```

The build uses the project `GameDir` property and deploys to:

```text
C:\Program Files (x86)\Steam\steamapps\common\Sephiria\AddOns\SephiriaOne\
  SephiriaOne.dll
  metadata.json
```

Visual Studio builds use the same MSBuild target. Press `Ctrl+Shift+B` to build;
use Rebuild Solution if Visual Studio skips an unchanged project and deployment
needs to run again. The deployment target excludes design-time builds and applies
to both Debug and Release configurations when the build target executes.

To run deployment as a Visual Studio command:

1. Open `SephiriaOne.slnx`. If necessary, right-click the `SephiriaOne` project
   and choose **Set as Startup Project**.
2. Choose **Debug** or **Release** in the configuration dropdown, then select
   **Deploy Mod** in the dropdown beside the Start button.
3. Press **Ctrl+F5** (**Debug > Start Without Debugging**).

The profile launches Windows PowerShell and runs `scripts/Deploy-Mod.ps1`. It
passes the selected configuration's exact DLL path and the project's `GameDir`,
so an existing Release DLL does not override the selected Debug build. Visual
Studio normally builds before starting; if that setting is disabled, build first.
The existing post-build target may also deploy during that build; both invocations
copy the same files. The profile still runs when Visual Studio skips an unchanged
build. It deploys files and does not launch the game.

This solution uses an
[executable launch profile](https://github.com/dotnet/project-system/blob/main/docs/launch-profiles.md).
Visual Studio's
[`tasks.vs.json` tasks](https://learn.microsoft.com/en-us/visualstudio/ide/customize-build-and-debug-tasks-in-visual-studio)
are for Open Folder mode rather than this solution workflow.

Build without deployment:

```powershell
dotnet build .\SephiriaOne.slnx --configuration Release -p:DeployMod=false
```

Use a different installed game directory for both assembly references and deployment:

```powershell
dotnet build .\SephiriaOne.slnx --configuration Release '-p:GameDir=D:\SteamLibrary\steamapps\common\Sephiria'
```

Deploy an existing build to the default game directory without compiling again:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File .\scripts\Deploy-Mod.ps1
```

The script prefers Release whenever that DLL exists, even if Debug is newer.
It falls back to Debug when the Release DLL is absent. If neither exists, it
reports that a build or explicit path is required; it does not build automatically.
Append `-BinaryPath '<path-to-built-dll>'` to select a particular output instead.
The selected DLL must have `metadata.json` beside it.

Append `-GameDir 'D:\SteamLibrary\steamapps\common\Sephiria'` to override the
script's default. Automated builds continue to pass the exact `TargetPath` and
project `GameDir` explicitly, so they deploy the configuration just built.

The script stops on errors. A missing source file, locked destination, or lack of
write permission causes deployment to fail; an MSBuild invocation reports that
failure through its `Exec` task. Multi-file deployment is not atomic: a later copy
failure can leave an earlier file already updated.

## Loader and API findings

These findings came from inspecting the installed game assembly, with the
[official API reference](https://teamhoray.com/mod-api) as supporting documentation.

### Loader contract

- `HorayModBase`, `HorayModAPI`, `AddOnMetadata`, and `AddOnLoader` are global types
  inside `Assembly-CSharp.dll`. No separate `HorayModAPI.dll` is needed for this project.
- `AddOnLoader` scans immediate child directories under the game's `AddOns`
  directory. A DLL placed directly in the `AddOns` root is not a complete installation.
- The supported metadata fields are `modName`, `modVersion`, `modAuthor`,
  `dllFile`, and `entryClass`.
- Missing metadata has a folder-name fallback. Missing `dllFile` causes selection
  of the first top-level DLL, and entry-class discovery also has a fallback.
  Explicit metadata avoids relying on these choices.
- The entry class must be a concrete subclass of `HorayModBase`. The loader uses
  `Activator.CreateInstance`, so a public parameterless constructor is appropriate.
- The loader calls `LoadMod(Metadata)`, which sets the instance metadata and
  invokes `OnModLoaded()`.
- `HorayModBase` is not a `MonoBehaviour`. Add a separate Unity component when a
  feature needs Unity component callbacks such as `Update()`.
- The inspected loader does not implement an explicit dependency load order or
  automatically scan a `Libs` directory. MiraModBase provides a separate bootstrap
  that explicitly loads Harmony and other dependencies before its main mod.
- Renaming an addon folder inside `AddOns` does not disable discovery. Move it
  outside that directory when an isolated test requires disabling it.

### Initialization and cleanup

`GameDataLoader.Awake()` calls `AddOnLoader.LoadAll()` before initializing the game
databases. Subscribe during `OnModLoaded()` and perform database work in the
appropriate callbacks.

| API surface observed in the installed assembly | Intended use |
| --- | --- |
| `OnLoadItemDatabase`, `OnLoadWeaponDatabase`, `OnLoadCostumeDatabase` | Register or modify the respective content. |
| `OnLoadMiracleDatabase`, `OnLoadStatusDatabase`, `OnLoadKeywordDatabase`, `OnLoadPassiveDatabase`, `OnLoadPropDatabase` | Register or modify other supported database content. |
| `OnAllDatabasesReady` | Resolve relationships across initialized databases. |
| `OnLocalizationReady` | Register translated strings through `HorayModLocalizationContext`. |
| `OnStartGameServerside`, `OnStartGameClientside` | Separate server and client game-start hooks. |
| `OnStartSessionServerside`, `OnStartSessionClientside` | Session hooks with a Boolean argument named `isSavedSession` in the internal notifier. |
| `OnFloorAllocatedServerside`, `OnFloorAllocatedClientside` | Floor hooks carrying GUID, floor name, and `FloorGenerator`. |
| `OnPlayerEvade`, `OnGetDebuff` | Additional gameplay extension points. |
| `GridInventoryStartPermission`, `GridInventoryEndPermission` | Inventory permission hooks carrying inventory and player objects. |
| `RegisterNetworkPrefab`, `RegisterNetworkPrefabs`, `UnregisterNetworkPrefab` | Network prefab registration helpers. |

The listed events were confirmed in the assembly, not individually tested in-game.
`OnAllDatabasesReady` fires before some subsequent object-pool setup in
`GameDataLoader`; it does not mean every game subsystem has completed startup.

`HorayModLocalizationContext.AddText` accepts either
`(language, key, value)` or `(key, Dictionary<string, string>)`.

The inspected `ItemDatabase` provides `Register(ItemEntity)` and
`Modify(int, Action<ItemEntity>)`. Registration rejects duplicate IDs and skips
disabled items. Its load event runs after the initial vanilla database population.

`HorayModBase` exposes `OnModUnloaded()`, and `AddOnLoader` has `UnloadAll()`.
Actual teardown behavior has not been tested. The inspected
`GameDataLoader.OnDestroy()` does not itself call `AddOnLoader.UnloadAll()`.
Do not assume that returning to the title screen invokes addon cleanup or reloads
a changed DLL. Use a full game restart during development.

The official documentation is marked work in progress. One concrete discrepancy:
it lists `OnFloorAllocated` as unimplemented, while this installed assembly has
separate server/client variants. Verify signatures against the installed DLL
before implementing features based on examples.

## Decompilation and symbol inspection

This installation contains managed Unity/Mono assemblies. ILSpy successfully
reconstructed readable C# for the inspected types. Original PDB files are not
required to inspect class names, fields, signatures, or method IL. They would help
with source mapping and debugging, but decompilation does not recover the original
comments or exact source text.

The research used ILSpy CLI `9.1.0.7988` from its NuGet package, unpacked under:

```text
%TEMP%\sephiria-mod-research\ilspycmd\tools\net8.0\any\
```

Selected type exports are in `%TEMP%\sephiria-mod-research\game-types`. These are
temporary local research files, not repository dependencies. They may disappear
when temporary files are cleaned up.

To reproduce inspection with the same CLI version using an installed SDK and
.NET 8 runtime:

```powershell
dotnet tool install --global ilspycmd --version 9.1.0.7988

$managed = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed'
ilspycmd -t HorayModAPI -r $managed "$managed\Assembly-CSharp.dll"
ilspycmd -t ItemDatabase -r $managed "$managed\Assembly-CSharp.dll"
```

If `ilspycmd` is already installed, inspect its version before changing the
installation. The GUI can also open `Assembly-CSharp.dll` directly with its sibling
managed DLLs available for resolution. See the [ILSpy project](https://github.com/icsharpcode/ILSpy)
and [CLI documentation](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.ILSpyCmd/README.md).

Useful starting points for further research are `ItemEntity`, `Charm_Basic` and
existing `Charm_*` implementations, `WeaponDatabase`, `WeaponAddon`, `PlayerAvatar`,
and `DungeonManager`. These are research directions, not a claim that all their
behavior has already been examined.

Compile the addon against the original installed assemblies. Keep decompiled game
source and game DLLs out of source control and release archives. Inspection of
private members does not make them publicly callable; a feature may need a public
API, reflection, or a targeted runtime patch. Unity serialized assets also require
a separate inspection workflow from managed-code decompilation.

## Reference material collected

The observations below describe the repositories as inspected on the recorded date.

| Resource | Findings and use |
| --- | --- |
| [Official HorayMod API](https://teamhoray.com/mod-api) | Developer reference for lifecycle, database events, localization, and network prefab helpers. Compare it with the installed assembly because the API is evolving. |
| [Xetsumei profile](https://github.com/Xetsumei/) | Starting point for ModMaker, QoL, enemy modification, and DungreedEnemies repositories. |
| [Sephiria-ModMaker](https://github.com/Xetsumei/Sephiria-ModMaker) | Optional content editor and runtime. Current tree primarily contains binaries, metadata, and documentation; useful for authoring workflows and supported content categories. |
| [sephiriaQoL](https://github.com/Xetsumei/sephiriaQoL) | Feature and compatibility reference. Current tree primarily distributes compiled binaries and documentation. |
| [SephiriaEnemyModify](https://github.com/Xetsumei/SephiriaEnemyModify) | Enemy-modification reference, also primarily a binary/documentation distribution in the inspected tree. |
| [DungreedEnemies](https://github.com/Xetsumei/DungreedEnemies) | Additional Xetsumei content reference. Repository layout was checked; implementation was not deeply reviewed. |
| [MiraItemMod](https://github.com/Mira090/MiraItemMod) | Main source reference for content mods. `Core.cs` shows lifecycle and Harmony setup; `Data.cs` registers content; `Registries/` provides content helpers. The repository reports an MIT license. |
| [CustomCostumeAddOn](https://github.com/Mira090/CustomCostumeAddOn) | Smaller source example of event subscriptions, costume registration, runtime sprite loading, and cleanup. |
| [SephiriaChatTranslatorAddOn](https://github.com/Mira090/SephiriaChatTranslatorAddOn) | Focused source example of Harmony, configuration, coroutines, localization, and a web request. Its remote translation service is not needed by SephiriaOne. |
| [ArcaneForged](https://github.com/Mira090/ArcaneForged) | Source example of custom weapons, registration, Harmony patches, and networking integration. |
| [MiraModBase](https://github.com/Mira090/MiraModBase) | Bootstrap implementation explaining the extra loader DLL and explicit dependency loading used by Mira's distributions. |
| [Mira's Item Mod blog post](https://note.com/mira090/n/n6069655525d8) | Author's explanation of mechanics and balance decisions. Useful context for the source, rather than an SDK tutorial. |
| [Sephiria on Nexus Mods](https://www.nexusmods.com/games/sephiria) | Distribution and user-facing installation reference. Individual mods may use different loading approaches. |
| [DiceTalentMod on Nexus](https://www.nexusmods.com/sephiria/mods/18) | Concrete native AddOns packaging and session-load/logging instructions. |

Mira's projects contain developer-specific reference paths, including Harmony
paths outside their repositories. They should be adapted to this project's
`GameDir` configuration rather than copied unchanged. A reference to a Harmony DLL
under a MelonLoader directory in an example project does not make MelonLoader a
requirement for a native HorayMod addon.

## Development decisions and constraints

- Prefer the official events and database APIs for supported changes. Add a
  narrowly scoped [Harmony patch](https://harmony.pardeike.net/v2/articles/intro.html)
  when the required behavior lacks an appropriate hook. The starter project does
  not currently reference or ship Harmony.
- Treat dependency loading and packaging as explicit work when adding libraries.
  The current deployment script only handles the starter DLL and metadata.
- Keep content IDs and saved-state keys stable across releases. Test an existing
  save when adding persistent content, and use a backed-up save for development.
- Separate server-authoritative gameplay changes from local UI and visual effects.
  Matching content definitions and network prefab identifiers may be needed on
  every participant. No multiplayer compatibility has been established for this project.
- Do not assume an added `[SyncVar]` attribute will work in an ordinary DLL build.
  Mirror synchronization requires the appropriate generated or explicit code.
  Mira's [Charm_Kill_Luck example](https://github.com/Mira090/MiraItemMod/blob/master/MiraItemMod/Items/Charm_Kill_Luck.cs)
  includes explicit serialization methods; it is an advanced reference, not
  starter boilerplate to copy without checking the game's Mirror version.
- Clean up event subscriptions and owned Unity objects when applicable, and
  remove only this mod's Harmony patches. Session transitions still need testing.
- Code-only changes and runtime PNG loading can start without a Unity Editor
  project. Custom asset bundles introduce a separate authoring/build workflow.
- Recheck referenced signatures and patch targets after game updates. Record the
  tested game version or assembly fingerprint alongside future compatibility notes.
- Publish our own code/assets and required redistributable dependencies with their
  notices. Check each reference project's license before reusing its code or artwork.

## Next verification steps

1. Launch Sephiria and enter the town/lobby or a run; the title screen alone is
   insufficient for the documented addon-loading workflow. Fully restart to load
   the new `0.2.0` binary.
2. Inspect `Player.log` for these expected entries:

   ```text
   [SephiriaOne] Loaded v0.2.0
   [SephiriaOne] All databases ready
   [SephiriaOne] Blue local player name applied (#0000FF)
   ```

3. Follow the [blue-name visual checks](blue-player-name.md#in-game-verification)
   and record the result. The confirmed `0.1.0` lifecycle does not establish that
   the new color renders correctly.
4. Test panel reopening, scene/session transitions, and coexistence with RaidRaid.
   In multiplayer, check both host and client ownership and other players' colors.
5. Once the cosmetic feature is verified, select the next small feature and
   inspect only its relevant game types and reference implementation.
