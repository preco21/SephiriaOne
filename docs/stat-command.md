# Character stat commands

Version `0.36.0` adds 17 verified C-menu modifiers. See the
[expanded stat list and native visibility audit](visible-stat-modifiers.md) for
commands, units, synchronization behavior and exclusions. The original ten
modifiers below retain their existing behavior.

## Design

Add `/stats` to the existing local chat interceptor. Match Fountain's authority:
solo/host only, changing all currently spawned players after validating the entire
batch. Use native synchronized custom stats so guests do not need this addon.
Since `0.11.0`, add/subtract commands accumulate a displayed offset from each
character's native stats, excluding this addon's contribution. The host maintains
that offset when equipment, buffs, or multipliers change. Absolute `set` remains
a one-time adjustment per avatar. Successful commands are retained in memory and
inherited by newly ready players; see [session inheritance](session-inheritance.md).
Since `0.10.0`, `/one status` shows current values and tracked adjustments for all
ready players, and `/one save` stores active settings for future hosted sessions.
See [status and preset commands](session-preset.md) for saving and removal.

Examples: `/stats luck +10`, `/stats luck -5`, `/stats luck set 100`,
`/stats luck reset`, and `/stats reset` (all supported stats). `/stats list` lists
supported names and units. Bare numbers mean set; `add`, `sub`, and `subtract`
are also accepted. Explicit amounts are unsigned; shorthand `-N` means subtract.

Since `0.14.0`, `/stats luck x3` targets each character's native displayed luck
times 3, replacing previous addon intent. Repeating it does not stack. The host
maintains the factor after native changes. `x1` restores native; `x0` retains
zero where allowed. A delta after a factor starts a fresh native offset.
`set x3` also works from the panel's Set button. See [multipliers](multiplier-command.md)
for fractional factors, exact-result checks and saved presets.

Values use the selected character-panel metric, including its normal offset.
Critical chance and evasion rating support two decimal places; the others use
whole numbers. Evasion rating is not the derived dodge percentage. Bounds below
are addon command limits, not claims about game balance or universal game caps.

| Name | Native key | Command/display units | Allowed resulting value |
| --- | --- | --- | --- |
| luck | LUCK | Luck points | 0..10000 |
| defense | DAMAGEREDUCTION | Defense points, not damage reduction percent | 0..10000 |
| attackspeed | ATTACKSPEED | Total attack speed percent (100 is normal) | 1..1000 |
| critical | CRITICAL | Critical chance percent | 0..100 |
| criticaldamage | CRITICALDAMAGEBONUS | Bonus critical damage percent (50 is native default) | 0..10000 |
| evasion | EVASION | Evasion rating (not dodge chance) | 0..100 |
| cooldown | COOLDOWNRECOVERYSPEED | Cooldown recovery stat points | 0..10000 |
| mpregen | MPREGEN | MP regeneration stat points | 0..10000 |
| negotiation | NEGOTIATION | Negotiation points | 0..10000 |
| truedamage | TRUEDAMAGE | True damage points | 0..10000 |

Aliases: `crit`, `critdamage`, `armor`, `attack-speed`, `crit-chance`,
`crit-damage`, `cooldownrecovery`, and `mp-regen`. Arbitrary native keys, flags,
health, mana capacity, movement speed, and converted elemental damage are excluded:
they have different storage or calculation paths. Choices and Fountain retain
their existing commands.

`+10` then `+5` means native +15. `set 100` then `+10` switches back to native +10.
Native means the character's current value without this addon's tracked raw
contribution, including ordinary equipment and buffs, rather than a fixed snapshot
from session start. Canceling the net offset restores the exact raw baseline.
Set/add/subtract solve for the required integer base-stat change after accounting
for calculated bonuses and amplification.
Reject an exact target that integer rounding cannot represent; never silently
approximate or replace the player's multiplier. Reject non-positive multipliers
and native integer overflow. A range or representability failure for one player
rejects the whole batch before any write.

After a successful relative adjustment, changed native inputs trigger host-side
recalculation in `LateUpdate`. If the requested offset becomes unrepresentable or
outside the command bounds, remove only its addon contribution, warn once, and
retry when inputs change. `/one status` marks it as suspended. A failed initial
inheritance remains rejected until an explicit command succeeds. See the
[relative-stat audit](relative-stat-consistency.md) for arithmetic and limitations.

Track the signed net base-stat adjustment in `SEPHIRIAONE_STAT_<NATIVE_KEY>`.
Reset subtracts only that contribution, preserving later additive equipment/buff
changes. Reset bypasses command limits and amplification checks so native values
can be restored. Zero is a set value, not a reset. Markers live with the avatar;
new avatars and later joiners inherit the active session settings once. Reset
also clears the corresponding retained setting. Unloading does not undo
these adjustments, matching Fountain; reset explicitly before unloading if wanted.
Independent absolute overwrites of the same base stat cannot be reconstructed.

## Installed-game findings

Inspected Sephiria 1.0.33 on 2026-09-24 using the temporary ILSpy installation.
Assembly fingerprint is recorded in development-notes.md. No game source or DLLs
are included in this repository.

- `UnitAvatar.customStats`, `calculatedBonusStats`, and `customStatsAmp` are native
  synchronized dictionaries. `GetRawStatUnsafe` adds base and calculated bonus,
  multiplies by `100 + amplifier` using integer arithmetic, converts to float,
  divides by 100, and truncates to an integer.
- `AvatarStatsHooker` displays luck/defense/cooldown/MP regeneration/negotiation/
  true damage directly; critical and evasion divide by 100; attack speed adds
  100; critical damage adds 50. Evasion has a separate logarithmic dodge metric.
- `UI_StatsPanel` subscribes to custom-stat and calculated-bonus changes. Gameplay
  reads the same native stats. A live peer check is still required to establish
  observed UI refresh and gameplay behavior on another machine.
  The `0.11.1` [lifecycle audit](sync-lifecycle-audit.md) found that open panels
  do not subscribe to amplifier changes; reopening may be required despite
  correctly synchronized underlying values.
- The four elemental damage keys use a separate conversion formula, which is
  why they are not exposed by this command.

## Implementation plan

1. Add portable parser/catalog/planner tests for commands, units, aliases, exact
   amplified targets, mixed player values, batch rejection, overflow, and resets.
   Run tests to confirm the missing implementation fails.
2. Add `StatCommand.cs` (parsing/catalog) and `StatPlan.cs` (pure batch planning).
   Link them into the existing test harness and make the tests pass.
3. Add `CharacterStats.cs` for host authorization, ready-player collection,
   native dictionary writes, contribution markers, and local result messages.
   Extend `ModChatCommands.cs`; bump the project/metadata to 0.7.0.
4. Run portable tests, Debug/Release builds, installed-game compatibility checks,
   code review, and deployment hash checks. Record outcomes and remaining live
   checks, then commit and push the focused change using Conventional Commits.

## Live verification

Fully restart after updating. Record different player baselines, then try luck
set/add/subtract/reset with an unmodified guest. Verify both character panels and
luck-dependent gameplay. Test critical +5, attackspeed set 150, and evasion +1.5.
Change equipment between a command and reset; repeat reset and reset-all. Test
guest rejection, normal chat, malformed commands, an unrepresentable amplified
target, session transitions, and a joining player. Confirm other command families
and blue names still work. Live results have not yet been recorded.

## Verification results

On 2026-09-24, 117 character-stat checks passed, alongside all 156 existing checks
(273 total). Debug and Release builds completed with zero warnings/errors.
Installed-game candidate-guard compatibility checks and the embedded Harmony
runtime/license check also passed, covering the unchanged candidate feature.
An independent code review found no actionable issues.

Release 0.7.0 was deployed to `AddOns\SephiriaOne`. Both deployed files matched
their build-output SHA-256 hashes; the DLL assembly version is `0.7.0.0`.
These checks do not execute Unity gameplay, chat input, or remote clients. The
live checks above remain necessary after fully restarting the game.
