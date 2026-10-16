# PvP Evasion Cap Implementation Plan

> **For agentic workers:** Execute inline using the executing-plans workflow;
> verify each step before marking it complete. See current feature docs before
> reusing this historical plan.

**Goal:** Cap stat-based dodge chance at 50% during balanced PvP, as selected by
the user on 2026-10-17. Lower chances and temporary guaranteed-dodge items remain
native. Apply attacker anti-Evasion after the cap.

**Architecture:** Insert a scalar adapter after the native Evasion formula in
`UnitAvatar.ApplyDamage`, before its lower clamp and attacker reduction. Reuse
the existing hit context, current connection/participant policy and balancing
toggle. Never change character stats, damage objects, native RNG calls or persistent
state. This serves casual friendly fire and all shared match modes.

**Tech Stack:** C#, Harmony, installed Unity/Mono game contracts, executable
.NET combat fixtures.

## Constraints

- PvE in both directions, self/system hits, NPC victims and clients stay native.
- Keep guaranteed-dodge callbacks/consumption, parries, guard, dash invulnerability,
  MP-shield suppression and native elemental/fall damage exclusions intact.
- No new toggle, setting serialization, guest asset or network message.
- Build/test only with `-p:DeployMod=false`; do not deploy or launch the game.
- Conventional Commit and push after verification; no further approval needed.

## Implementation and verification

- [x] Extend `tests/SephiriaOne.FriendlyFireTests/Fixtures.cs` and a focused
  `EvasionFixtures.cs` with the inspected native formula/roll and deterministic
  RNG. Add `EvasionTests.cs` and register it in `Program.cs`. Reproduce a max-stat
  player dodging a 60% roll before the fix, then require that hit to land when
  balancing is active. Also test 40% rolls, lower stats, native stat reads,
  anti-Evasion, item dodges, policy transitions, nesting and exception cleanup.
- [x] Run `dotnet run --project tests/SephiriaOne.FriendlyFireTests -c Release
  -p:DeployMod=false`; observe the expected failed high-Evasion assertion.
- [x] Add `SephiriaOne/Features/Combat/FriendlyFireEvasion.cs` with
  `FriendlyFireRuntime.EvasionChance(float)` and an exact IL-boundary validator.
  In an eligible balanced hit return `Math.Min(nativeChance, 50f)`; otherwise
  return the original input. Compose it in `FriendlyFireTranspiler.Rewrite`.
  Reject missing/duplicated/changed formula anchors at installation.
- [x] Add installed-game contracts in `GameEvasionTests.cs`, register in
  `GameFriendlyFireCompatibilityTests.cs`, and check that all native instructions
  and RNG calls remain unchanged except the one inserted adapter. Mutate the
  formula/anchor to verify rejection. Exercise the combined transpiler too.
- [x] Update EN/KO command help, feature/domain documentation and version. Verify
  the Release build, executable combat tests (including warm allocations), and
  portable/installed-game contracts with the installed Harmony compatibility DLL.
- [x] Request read-only review and resolve findings. Review found no code issue;
  corrected documentation to distinguish preserved RNG calls from the number
  of conditional draws after a changed dodge result.

Finish with the repository's authorized Conventional Commit/push workflow.
Report the remaining live host/stock-guest checks and that no deployment occurred.

The design has been checked against the approved limit and existing combat
architecture. Restoration is immediate because the adjustment only transforms
one calculation result inside an admitted hit; there is no restoration journal.
