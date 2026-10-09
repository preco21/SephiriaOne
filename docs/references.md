# Collected references

Indexed 2026-10-09 (Asia/Seoul). These links consolidate material already recorded
in repository investigations; this edit does not revalidate current external
page contents. An external example is a research lead, not a compatibility
contract for the installed game. Start with the [index](README.md) and verify
native behavior against the installed assemblies/assets and current source.

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
