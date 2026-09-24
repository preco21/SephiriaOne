# Native-baseline command multipliers

## Contract

The user confirmed that `xN` replaces previous addon intent and targets each
player's native displayed baseline times N. Native luck 10/20 becomes 30/60
with `x3`; repeating it, or entering `x2` then `x3`, ends at 30/60.
The baseline excludes this addon's tracked contribution and includes current
native bonuses and amplifiers. Display offsets matter: native attack speed
100% times 2 means 200%, not a doubled raw zero.

- Support `/stats <stat> xN` and `/fountain xN`, case-insensitively.
- Accept `set xN` too, allowing the existing panel's Set button to share the
  parser. Reject `add xN`/`sub xN` rather than infer contradictory operations.
- Factors are invariant unsigned decimals 0..10000 with at most two decimals.
  Preserve existing result bounds and reject fractional integer outcomes or
  native-amplifier results that cannot be represented exactly. No rounding.
- `x1` removes multiplier intent and restores the exact native baseline; zero
  remains a real multiplier, subject to each feature's normal result limits.
- A numeric Add/Subtract after multiplier mode starts a new native-relative
  offset. Subsequent deltas accumulate. Existing non-multiplier semantics stay
  unchanged, including Fountain's historical Set-then-Add behavior.
- `/choices` continues to mean extra candidates. Its native extra baseline is
  normally zero and total offers vary by generator. Reject multiplier syntax
  with an explanation; do not redefine it as multiplication of current extras.
- The five researched resource settings remain unimplemented.

## Design choice

A one-time computed offset loses the factor after native changes; multiplying
the currently modified value compounds on retries. Retain a multiplier mode in
the shared policy, evaluate from tracked native state, and reconcile on input
changes instead. Reuse the stats planner, coordinator, guarded native writes,
all-player preflight and fault recovery. Add Fountain multiplier maintenance
before its existing carryover-limit rule.

Status and explicit save expose retained factors. Write preset v2 only when a
multiplier exists; continue reading/writing v1 for older setting types. v1 must
not silently accept the new mode. Read-only status must remain read-only.
Host authority, joining players, reset, restart and unmodified guests use the
same native transport as existing commands. No custom guest protocol or hotkey.

## Implementation plan

1. Add portable multiplier tests and observe rejection of `x3` before changes.
   Add a reusable parser/validator in `Controls/RelativeMultiplier.cs`, multiply
   operations in stats/Fountain, policy mode and planner support. Verify native
   10/20 -> 30/60, offsets, fractions, bounds, transitions, reset and idempotence.
2. Extend versioned presets and shared status; preserve every old v1 test while
   replacing its unknown-version fixture with v3. Round-trip retained factors
   against different joining baselines and reject malformed v1/v2 factors.
3. Add runtime tests through `SettingsActions.Execute`: host/guest authority,
   live baseline changes, late join, restart, rejected batch, suspension/recovery
   and save/reload. Observe missing Fountain maintenance before implementing
   its coordinator rule. Keep carryover limits fresh before native reads.
4. Expose `xN` through panel Set without new controls. Update help, user docs,
   development history and version to 0.14.0. Run both test executables, Debug
   and Release builds with `-p:DeployMod=false`, native compatibility checks,
   final diff review and conventional commit/push. Never deploy.

Verification commands (from the repository root):

```powershell
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false
dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false
dotnet build SephiriaOne.slnx -c Debug --nologo -p:DeployMod=false
dotnet build SephiriaOne.slnx -c Release --nologo -p:DeployMod=false
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false -- 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed' '.\SephiriaOne\bin\Release\netstandard2.1\SephiriaOne.dll'
```

## Review focus

Verify baseline removal before multiplication, display offsets, exact arithmetic,
preserved legacy Fountain behavior, no multiplier stacking after joins/restarts,
all-player rejection before writes, suspension recovery after native changes,
retained zero versus identity, preset version boundaries, and panel/chat parity.

## Completion record

All four implementation steps are complete. The first portable test failed at
`/stats luck x3` parsing; after implementation, all multiplier planner and preset
tests passed. Runtime tests first failed on Fountain recalculation after native
capacity changed, then passed after its ordered maintenance rule was installed.
Identity fault recovery also failed before its reset-equivalent guard was added.
Final comparison added a regression for legacy negative-baseline join rejection;
it failed before restoring the original nonnegative policy check, then passed.

Verified 750 portable checks and 354 runtime fixture checks, Debug/Release builds
with deployment disabled, and installed-native compatibility. Independent review
found no actionable issues. No game execution, real profile/preset modification
or deployment occurred.
The main checkout was clean at task start; changes remain within the authorized
repository. All new tests use existing isolated temporary fixture storage.
