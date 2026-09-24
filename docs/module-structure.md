# Module structure

Source files are grouped by feature and shared responsibility. The initial folder
refactor preserved behavior; `0.12.0` adds reusable synchronization modules.
The SDK project discovers source files recursively; namespaces remain stable.

```text
SephiriaOne/
  Entry.cs
  SephiriaOne.csproj
  metadata.json
  Chat/
  Features/
    Choices/
    Fountain/
    Names/
    Stats/
  Infrastructure/
  Session/
    Presets/
  Synchronization/
    Core/
    Game/
    Presentation/
  Properties/
```

| Location | Responsibility |
| --- | --- |
| `Entry.cs` | Addon lifecycle and creation/shutdown of feature controllers. |
| `Chat/` | Local chat interception and dispatch to feature commands. |
| `Features/Choices/` | Candidate command parsing/planning, host writes, and Harmony generation guards. |
| `Features/Fountain/` | Fountain command parsing/planning, point writes, and carryover-cap operations. |
| `Features/Names/` | Gradient formatting, local labels, and synchronized name state. |
| `Features/Stats/` | Character stat catalog, commands, exact arithmetic, and host batch updates. |
| `Infrastructure/` | Shared embedded Harmony runtime loading. |
| `Session/` | Policy, registered inheritance/maintenance rules, scope lifecycle, diagnostics, and critical native read boundaries. |
| `Synchronization/Core/` | Unity-independent reconciliation, reference identity, outcome records, and journaled write batches. |
| `Synchronization/Game/` | Shared host readiness, player/native snapshots, preflight/readback, and native write helpers. |
| `Synchronization/Presentation/` | Registered active bindings and safe native stat-panel refresh. |
| `Session/Presets/` | Saved policy codec/storage and status/save/forget commands. |
| `Properties/` | Visual Studio launch configuration. |

Session partial classes stay together under `Session/`, including preset-related
parts in its `Presets/` subfolder. Feature folders own their command syntax and
calculations; session code coordinates their application across players/events.

All production types retain the `SephiriaOne` namespace. In particular, the
metadata entry type, reflection-based compatibility checks, Harmony ownership,
embedded-resource names and partial-class identities remain stable. These folders
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

See the [extension guide](synchronization-guide.md) for adding rules and bindings.

## Initial folder-refactor verification (0.11.1)

The refactor moves 27 production source files and 11 portable test files without
changing their contents. Verification compares moved files with their original
Git blobs, validates test source links, runs Debug/Release builds with deployment
disabled, and runs the existing portable/runtime and installed-game checks.

All 38 content comparisons and linked-source checks passed. Debug and Release
builds completed with zero warnings/errors; all 792 existing checks and the
installed-game compatibility checks passed. Installed addon DLL/metadata hashes
remained unchanged. No deployment occurred.
