# Module structure

Source files are grouped by feature and shared responsibility. The initial folder
refactor preserved behavior; `0.12.0` added reusable synchronization modules.
Version `0.12.2` removes the local-only presentation layer and keeps native
multiplayer synchronization.
Version `0.13.0` adds shared settings controls and an addon-owned host panel.
The SDK project discovers source files recursively; namespaces remain stable.

```text
SephiriaOne/
  Entry.cs
  SephiriaOne.csproj
  metadata.json
  Chat/
  Controls/
  Features/
    Choices/
    Fountain/
    Merchants/
    Names/
    Rabbit/
    Resources/
    Stats/
  Infrastructure/
  Localization/
    Catalogs/
  Session/
    Presets/
  Synchronization/
    Core/
    Game/
  Properties/
  UI/
```

| Location | Responsibility |
| --- | --- |
| `Entry.cs` | Addon lifecycle and creation/shutdown of feature controllers. |
| `Chat/` | Local chat interception and forwarding to the shared settings dispatcher. |
| `Controls/` | Shared action dispatch/results and immutable settings/player snapshots used by chat and UI. |
| `Localization/` | Cached English/Korean JSON catalogs, validated formatting, local config, atomic reload and presentation revision notifications. |
| `UI/` | Addon-owned host panel, pause-menu binding, native control-stack lifetime, widgets and session-bound drafts. |
| `Features/Choices/` | Candidate command parsing/planning, host writes, and Harmony generation guards. |
| `Features/Fountain/` | Fountain command parsing/planning, point writes, and carryover-cap operations. |
| `Features/Merchants/` | Host encounter toggle/chance parsing, native room placement, run-scoped spawn bookkeeping and actor-specific crime/retaliation guards. |
| `Features/Names/` | Gradient formatting, native publication controller, and synchronized name state. |
| `Features/Rabbit/` | Potion option parsing/settings, native drink and earned-level hooks, audited reward catalog, costume starting artifact and host description adapter. |
| `Features/Resources/` | Starting grants, capacity/budget policy and native resource boundaries. |
| `Features/Stats/` | Character stat catalog, commands, exact arithmetic, and host batch updates. |
| `Infrastructure/` | Shared embedded Harmony runtime loading. |
| `Session/` | Policy, registered inheritance/maintenance rules, scope lifecycle, diagnostics, read-only settings capture/change notifications, and critical native read boundaries. |
| `Synchronization/Core/` | Unity-independent reconciliation, reference identity, outcome records, and journaled write batches. |
| `Synchronization/Game/` | Shared host readiness, player/native snapshots, preflight/readback, and native write helpers. |
| `Session/Presets/` | Saved policy codec/storage and status/save/forget commands. |
| `Properties/` | Visual Studio launch configuration. |

Session partial classes stay together under `Session/`, including preset-related
parts in its `Presets/` subfolder. Feature folders own their command syntax and
calculations; session code coordinates their application across players/events.

All production types use the `SephiriaOne` namespace. The metadata entry type,
gameplay Harmony ownership, embedded-resource names and partial-class identities
remain stable. `MultiplayerNameController` replaces the former local color
controller. Version `0.16.0` adds a narrow host-local costume-description hook for
the requested Rabbit options; it does not style guest UI. These folders
do not create separate assemblies or introduce new dependency boundaries.

The portable test project mirrors `Features/` and `Session/Presets/`; installed
game inspection belongs in `Compatibility/`. Its entry point stays at the test
project root. The runtime fixture runner and fixtures remain together in
`SephiriaOne.RuntimeTests`. Both projects explicitly link the production files
they test, with matching virtual paths under `Source/` in Visual Studio. Update
those `Compile Include` and `Link` paths when moving shared source files.

Deployment paths are unchanged: the project, metadata, build output directory,
launch profile and deployment script retain their previous locations. Build with
`-p:DeployMod=false` when deployment is not intended.

See the [extension guide](synchronization-guide.md) for adding native state rules.

## Initial folder-refactor verification (0.11.1)

The refactor moves 27 production source files and 11 portable test files without
changing their contents. Verification compares moved files with their original
Git blobs, validates test source links, runs Debug/Release builds with deployment
disabled, and runs the existing portable/runtime and installed-game checks.

All 38 content comparisons and linked-source checks passed. Debug and Release
builds completed with zero warnings/errors; all 792 existing checks and the
installed-game compatibility checks passed. Installed addon DLL/metadata hashes
remained unchanged. No deployment occurred.
