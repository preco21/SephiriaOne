# Collected references

Initially indexed 2026-10-09 (Asia/Seoul). Official developer materials were
checked and added on 2026-10-30; older entries retain their original inspection
context. An external example is a research lead, not a compatibility contract
for the installed game. Start with the [index](README.md) and verify native
behavior against the installed assemblies/assets and current source.

## Official developer guide and materials

All contributors, including AI agents, must follow TEAM HORAY's
[official guide and rules](https://github.com/team-horay/sephiria_modding/tree/main).
Use these as the primary external reference; community examples supplement them.

| Material | Use |
| --- | --- |
| [Official repository](https://github.com/team-horay/sephiria_modding/tree/main) / [English guide](https://github.com/team-horay/sephiria_modding/blob/main/README.en.md) | Developer rules and documentation entry point. |
| [Getting started](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/getting-started.md) / [metadata](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/metadata.md) | Local setup, package metadata and dependencies. |
| [DLL guide](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/dll-mods.md) | C# project setup, lifecycle and debugging. |
| [API reference](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/api/README.md) | Public APIs, mod options and UI. |
| [Examples and templates](https://github.com/team-horay/sephiria_modding/tree/main/examples) / [DLL sample](https://github.com/team-horay/sephiria_modding/tree/main/examples/dll-sample) | Official implementation starting points. |
| [Localization](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/localization-mods.md) / [costumes](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/costume-mods.md) | Content formats and authoring. |
| [Publishing](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/publishing.md) / [FAQ](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/faq.md) | Workshop distribution, DLL review and multiplayer requirements. |

The developer rules require noncommercial mod creation/distribution and prohibit
redistributing extracted game assets. Consult the full rules before contributing.

The current [DLL guide](https://github.com/team-horay/sephiria_modding/blob/main/docs/ko/dll-mods.md)
uses `WorkshopModBase` and `WorkshopLocal`; this project's existing loader uses
`AddOns`. Historical host-only findings do not establish Workshop compatibility.
Check the targeted game/API version and official multiplayer requirements before
adapting examples or changing packaging.

## SDK, loading and implementation examples

These were collected in [development notes](development-notes.md#reference-material-collected).
Repository-layout/license observations there describe the original inspection,
not necessarily the latest upstream revision.

| Source | Purpose and limits |
| --- | --- |
| [Official HorayMod API](https://teamhoray.com/mod-api) | Lifecycle, database, localization and prefab API reference. Recorded as work in progress; the installed API differed on floor events, and later access was unavailable during the panel investigation. |
| [Xetsumei repositories](https://github.com/Xetsumei/) | Starting point for other Sephiria mods and tools. |
| [Sephiria-ModMaker](https://github.com/Xetsumei/Sephiria-ModMaker) | Content authoring/runtime reference; custom content compatibility is distinct from this project's native host-side effects. |
| [sephiriaQoL](https://github.com/Xetsumei/sephiriaQoL) | Feature/packaging reference; the inspected tree primarily distributed binaries/docs. |
| [SephiriaEnemyModify](https://github.com/Xetsumei/SephiriaEnemyModify) | Enemy modification reference; likewise mainly binaries/docs at inspection. |
| [DungreedEnemies](https://github.com/Xetsumei/DungreedEnemies) | Additional content reference; layout was checked, implementation was not deeply reviewed. |
| [MiraItemMod](https://github.com/Mira090/MiraItemMod) | Lifecycle/Harmony setup, content registration and helpers. The original investigation recorded an MIT license; verify applicable upstream terms before copying code. |
| [CustomCostumeAddOn](https://github.com/Mira090/CustomCostumeAddOn) | Costume registration, subscriptions, sprite loading and cleanup examples. |
| [SephiriaChatTranslatorAddOn](https://github.com/Mira090/SephiriaChatTranslatorAddOn) | Configuration, Harmony, coroutines and web requests. Its translation service is not a dependency here. |
| [ArcaneForged](https://github.com/Mira090/ArcaneForged) | Custom weapons, registration and networking examples. |
| [MiraModBase](https://github.com/Mira090/MiraModBase) | Explicit dependency bootstrap; not an automatic native loader feature. |
| [Mira's Item Mod blog](https://note.com/mira090/n/n6069655525d8) | Author's gameplay/balance context, not an SDK contract. |
| [Sephiria Nexus category](https://www.nexusmods.com/games/sephiria) | Distribution/install examples; different mods use different loaders. |
| [SephiriaChoiceExpander](https://www.nexusmods.com/sephiria/mods/19) | Inspiration for extra candidate counts. Its BepInEx distribution is not used by this addon; see [our implementation](choice-command.md). |
| [DiceTalentMod](https://www.nexusmods.com/sephiria/mods/18) | Native AddOns packaging and session-load/logging reference from the initial investigation. |

## Game data and gameplay research

| Source | How it was used |
| --- | --- |
| [Sephiria Tools statuses](https://www.sephiria.tools/statuses) | Names/units for the [visible-stat audit](visible-stat-modifiers.md). It includes internal effects, so actual `C` menu assets decide eligibility. |
| [Namu Sephiria section 4.1](https://en.namu.wiki/w/%EC%84%B8%ED%94%BC%EB%A6%AC%EC%95%84#s-4.1) | User-supplied lead that could not be retrieved during the stat audit; not used as verified evidence. |
| [Sephiria World potions](https://sephiria.world/potions) | Starting list for [Rabbit level-up rewards](rabbit-level-up-potions.md), cross-checked against installed potion definitions/effects. |

The local game evidence is usually more specific: `Assembly-CSharp.dll` for code,
Mirror/FizzySteamworks assemblies for networking, and scenes/resources assets for
UI rows, costumes and item definitions. The original assembly fingerprint and
inspection workflow are recorded in [development notes](development-notes.md).
Do not publish the game binaries, decompiled exports or private logs with these docs.

## Tools and native networking contracts

| Source | Related repository research |
| --- | --- |
| [ILSpy](https://github.com/icsharpcode/ILSpy) and [CLI documentation](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.ILSpyCmd/README.md) | [Managed-code inspection](development-notes.md#decompilation-and-symbol-inspection); pinned original CLI version is recorded there. |
| [Harmony introduction](https://harmony.pardeike.net/v2/articles/intro.html) | Narrow native hooks; see [embedded dependency/license notes](third-party-notices.md). |
| [Mirror custom spawn functions](https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-spawnfunctions) | Why [new healing books](healing-item-investigation.md) need guest definitions/spawn registration. |
| [Unity UI comparison](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html) | Background for [panel investigation](control-panel-investigation.md); installed components, not a generic Unity manual version, determine integration. |
| [uGUI Toggle](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.UI.Toggle.html) and [IDragHandler](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/api/UnityEngine.EventSystems.IDragHandler.html) | Consulted for the [UI revamp investigation](ui-revamp-investigation.md) on 2026-10-09; installed Unity assemblies also confirmed no-notify toggle/slider setters and pointer-drag support. |
| [Input System action bindings](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/manual/ActionBindings.html) and [Keyboard API](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.11/api/UnityEngine.InputSystem.Keyboard.html) | Consulted 2026-10-10 for the [local UI hotkey](tabbed-settings-ui.md); installed native rebinding code and serialized title/session Settings layouts determine actual integration. |
| [Visual Studio launch profiles](https://github.com/dotnet/project-system/blob/main/docs/launch-profiles.md) and [Open Folder tasks](https://learn.microsoft.com/en-us/visualstudio/ide/customize-build-and-debug-tasks-in-visual-studio) | [Development setup](development-notes.md); solution launch profiles and Open Folder tasks are different mechanisms. |

Transport-specific evidence and its original API citations are preserved in
[disconnect investigation](disconnect-investigation.md) and
[penalty-stat review](penalty-stat-sync-review.md). Retain the distinction between
code-path evidence, a host log and an observed guest failure.

## GitHub release updater

See [implementation and publishing requirements](github-updates.md) and
[the original investigation](github-release-updates-investigation.md).

| Source | Purpose |
| --- | --- |
| [SephiriaOne releases](https://github.com/preco21/SephiriaOne/releases) | Actual distribution source. Repository code version is not proof that its release is published. |
| [Latest release API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release) | Stable-release discovery contract. |
| [Release assets API](https://docs.github.com/en/rest/releases/assets#get-a-release-asset) | Asset metadata and download contract. |
| [REST best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api) and [rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api) | Conditional requests, retry/backoff and request budgets. |
| [Immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases) | Publisher-side integrity context; not proof that every release is immutable or independently signed. |

For future investigations, record the date/revision consulted, native symbols,
what the source actually supports and any inaccessible/unverified material.
Link that feature record here instead of treating this catalog as newly verified
upstream documentation.
