# Candidate choice commands

Implemented in `0.5.0` on 2026-09-24 (Asia/Seoul).
Explicit reset commands added in `0.6.0`; see [reset behavior](command-reset.md).
Since `0.9.0`, active extra-candidate settings also apply once to newly ready
players; see [session inheritance](session-inheritance.md).

## Design and scope

Implement the user's requested item, weapon, and miracle candidate expansion in
the native HorayMod addon, retaining host-only commands and all-current-player
scope from `/fountain`. The reference is
[SephiriaChoiceExpander](https://www.nexusmods.com/sephiria/mods/19), whose page
describes five extra candidates and configurable amounts. This implementation
uses our own code and the installed game's APIs.

The number means **additional choices**, not a replacement for each reward's
base count. No bonus applies until a command is issued.

The `xN` syntax introduced for [stats and Fountain](multiplier-command.md) is
explicitly unsupported here: native extra-choice baselines are normally zero,
and total offer sizes depend on the generator. Use ordinary Set/Add/Subtract.

| Command | Effect for every currently spawned player |
| --- | --- |
| `/choices all 5` | Set this addon's bonus to five for all three categories. |
| `/choices item +2` | Add two to this addon's item bonus. |
| `/choices weapon -1` | Subtract one from this addon's weapon bonus. |
| `/choices miracle set 5` | Set this addon's miracle bonus to five. |
| `/choices all 0` | Remove this addon's bonuses, retaining other stat sources. |
| `/choices reset` or `/choices all reset` | Restore all three categories without this addon's bonuses. |
| `/choices item reset` | Restore one category; also supports `weapon` and `miracle`. |
| `/choices` | Show usage. |

`set`, `add`, `sub`, and `subtract` are accepted with unsigned amounts, just as
in `/fountain`. Targets are `all`, `item`, `weapon`, and `miracle`. Names are case
insensitive; only the exact command token is consumed. Bonus values and resulting
effective extra-choice stats must be between 0 and 20. Invalid input, arithmetic
overflow, non-host execution, or an initializing player rejects the whole batch
before any writes.
Reset and set-to-zero remove only the recorded addon contribution, so expansion
bounds do not prevent restoring native stats outside `0..20`. Reset remains
available if the candidate generation guards could not initialize.

The command applies to newly generated rewards and normal rerolls. Reopening an
already generated offer does not generate new candidates. Weapon choices mean
**anvil upgrade candidates**, not additional weapons on town stands. The native
choice count is naturally limited by the available eligible content.

The `0.11.1` [lifecycle audit](sync-lifecycle-audit.md) confirms native costume/
passive changes preserve raw candidate contributions, which scale with native
multipliers. It also found a native anvil UI edge: increasing choices after
opening an anvil can hide its reroll button while keeping its old shorter list.
Use a fresh anvil; the host addon does not invalidate a guest's cached offers.

## Installed-game findings

- `Sephirite.GenerateItems` uses `EXTRAITEMCHOICES`; ordinary rewards start at
  five before modifiers. Rewards are sent using a native synchronized list.
- `Anvil.HandleInteraction` and `Reroll` use `EXTRAWEAPONCHOICES`. Client-side
  generation caps the count to the current weapon's enhancement list size.
- `MiracleSelector2.GenerateMiracles` uses `EXTRAMIRACLECHOICES` with a base of
  three. The server sends the resulting metadata through its native target RPC.
- These stats reside in `UnitAvatar.customStats`, an existing synchronized
  dictionary. That also gives unmodified guests the stat used by their anvils.
- The effective stat includes `calculatedBonusStats` and percentage amplification.
  Validation must check the arithmetic the game actually uses.
- Item generation retries missing rarities without an exhaustion exit. Miracle
  generation refills its weighted pool without handling exhaustion of the full
  distinct list. Increasing candidate counts can expose these bugs.

## Approach

Use the existing synchronized stats, recording this addon's contribution in
namespaced keys in the same dictionary. Updating only the difference preserves
bonuses from equipment and other game systems, while dictionary resets also
discard the contribution markers. Writes originate from local host commands
and the retained host settings when new avatars become ready.

Two narrow Harmony transpilers add exhaustion guards to item and miracle loops.
Item generation also has a bounded retry count for a depleted available rarity.
The original selection, weighting, unlock filters, costs, and network messages
remain in use. If the expected method shape changes, disable `/choices` and log
the incompatibility instead of enabling unguarded expansion. Ship pinned Harmony
inside the addon DLL, load it before patch installation, and include its license.

A stats-only implementation would retain the exhaustion failures. Replacing
reward generation would duplicate the game's selection and networking rules.
The chosen small guards address the observed failure while keeping those rules.

Bonuses are runtime changes, not profile upgrades. Late joiners and replacement
avatars inherit the active extra-candidate settings once. Unloading removes this
addon's contributions before removing its patches. No generated offers are
rerolled for free or deleted as a side effect of changing a setting.

## Implementation plan

Use the writing-plans and test-driven-development workflows for these steps.

- [x] Add `ChoiceCommand.cs` and portable tests: recognition, target selection,
  signed shorthand, aliases, bounded amounts, preservation of non-mod bonuses,
  repeat-set idempotence, modifiers, overflow, and failed-batch behavior.
- [x] Add tested `ChoiceTranspilers.cs` loop guards and `ChoiceSafety.cs` runtime
  adapters. Match unique verified IL patterns and reject missing/ambiguous ones.
  Pin and embed Harmony `2.4.2` (Mono/.NET Framework build) with its license;
  isolate dependency loading from Harmony-referencing methods.
- [x] Add `ChoicePoints.cs` host service that plans every player/category before
  writing synchronized stats and contribution markers. Integrate both command
  families into one `ModChatCommands` listener and the addon lifecycle.
- [x] Build Debug and Release, run the portable suite, check the transpilers
  against the installed game's method bodies, review the feature, verify deployed
  hashes, update history/status, then commit and push with Conventional Commits.

## Verification results

- Debug and Release builds passed with zero warnings and errors.
- 117 automated checks passed: 21 name synchronization, 40 Fountain, 48 candidate
  command/planner, and 8 generation-guard checks. The guard tests patch executable
  fixtures and exercise empty/insufficient pools, ordinary counts, and rejection
  of incompatible method shapes.
- Both transpilers matched the installed game's actual method bodies without
  executing game code. The compatibility check also confirmed the embedded
  Harmony runtime and license. This does not exercise Unity's Mono patch startup.
- Code review found no actionable issues. Release DLL and metadata hashes matched
  the deployed `AddOns\SephiriaOne` files; assembly version is `0.5.0.0`.
- The only output DLL is `SephiriaOne.dll`. Harmony is embedded, and its runtime
  references only framework assemblies; no game DLL or extra loader is shipped.

Run the portable tests:

```powershell
dotnet run --project .\tests\SephiriaOne.Tests --configuration Release
```

After building, also check compatibility against an installed game:

```powershell
dotnet run --project .\tests\SephiriaOne.Tests --configuration Release -- `
  'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed' `
  '.\SephiriaOne\bin\Release\netstandard2.1\SephiriaOne.dll'
```

## Live verification

After a full game restart, use `/choices all 5` before opening a new reward.
Check item rewards, anvil upgrades, and miracles in solo and with an unmodified
guest. Compare with `/choices all 0` on newly generated offers. Test per-category
set/add/subtract, rejected guest commands, invalid values, repeated commands,
normal chat, and `/fountain`. Check low-content pools and rerolls for hangs or
exceptions. Check panel navigation with more candidates and avatar/session
transitions. Automated checks cannot establish Unity rendering or peer behavior.
