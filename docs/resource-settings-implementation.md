# Resource settings implementation

Continue the [native investigation](resource-settings-investigation.md) on
`0.14.0`. The user confirmed future fresh-run grants only for dice and leaves.
Implement all five controls through `/resources`, the host panel, explicit
presets and existing native host synchronization, including unmodified guests.
No deployment, new hotkeys or real profile writes during development.

## Semantics

- Set means total starting allowance/capacity/budget. Add/subtract accumulate
  from each character's native baseline; after Set or xN they start a new offset.
  `xN` replaces intent, using the confirmed native-baseline multiplier semantics.
- Dice and leaves affect future fresh grants, never refilling current balances.
  Native saved zero differs from absence. Freeze a starting policy when its
  initial grant begins, so later commands do not alter a half-consumed grant.
- Slots, talents and fruit apply to ready players, joins and native changes.
  Set establishes an owned contribution; later native gains remain intact.
  Multipliers track native baselines. Reductions reject occupied/allocated state
  before writes. Fruit changes do not replay already consumed run benefits.
- Preserve inventory checkpoint capacity before native item enumeration, even
  if the future preset was reduced/forgotten. A reset must not drop saved items.
- Talent budget must be available before saved allocations are clamped. Stop an
  incompatible load explicitly rather than silently lose allocations.
- Leaves transform the two identified starting grants, never arbitrary wallet
  mutations. Preserve intervening spending/income and settle new mid-run arrivals
  once using their native initialization allowance. Reconnect restores saved cash.
- Safe numeric limits are addon limits, not claims about native hard caps.

## Shared interfaces and steps

1. Pure resource catalog, command, setting/policy and planner. Reuse factor
   parsing, exact native-stat arithmetic, checked integers and all-player
   preflight. Write failing portable tests before implementing these types.
2. Typed native adapters with early initialization readiness separate from
   normal host commands. Journal ready-player writes and expose diagnostics;
   keep session/policy lifetime and once-only grant state separate.
3. Validate and install exact native initialization, inventory-restore,
   talent-load and departure boundaries. Keep namespaced metadata in native run
   checkpoints for ownership/grant state, never guest profile selections.
4. Extend versioned presets without losing v1/v2 fields. Add shared action
   dispatch and a Resources panel tab with total/spent/current/next-start labels.
5. Portable and runtime fixture tests: reset/mode transitions, old presets,
   distinct baselines, late join/reconnect, repeated runs, saved zero, occupied
   shrink, saved inventory beyond native size, early talent loads, native changes,
   zero fruit allowance, grant idempotence, overflow and partial-write recovery.
6. Build Debug/Release with `-p:DeployMod=false`; inspect installed-game IL
   boundary compatibility; review and document limits, then conventional commit
   and push. Keep decompiled source and research tools outside the repository.

## Work ownership

Core/policy/inventory/UI integration is handled in the main task. Bounded
parallel work owns only new starting-resource and budget adapter/hook files.
Shared files are changed centrally, and the integrated build/tests are the
acceptance gate. Live Unity/multiplayer behavior remains a separate manual check.

## Completion and review record (0.15.0, 2026-09-24)

All five controls are implemented through `/resources` and the Resources panel
tab. `/one` replaces `/mod` with no alias, preserving the generic token for other
addons. The explicit preset codec writes v3 for resource settings and retains
v1/v2 reading/writing for older settings. See [usage](resource-command.md).

Review reproduced and patched these paths:

- The live Resources warning came from `MissingMethodException` for Harmony's
  `LoadsConstant(CodeInstruction, string)`, absent in the HarmonyX assembly loaded
  by another addon. Direct string-load IL matching removes that dependency.
  Starting, inventory and budget guards now install independently, and their
  live availability/reason is shown instead of a stale generic flag.
- Resource maintenance only processes successfully enrolled resource kinds. A
  rejected or faulted combined inheritance cannot apply a resource-only subset.
  Explicit commands still enroll their own supported family after another
  family's inheritance was rejected; existing Fountain behavior is retained.
- Shared command journals capture run-save identity and run generation. Early
  resource journals additionally capture player IDs and native object lifetimes.
  Recovery cannot replay old values into a replaced player, storage or run.
- Native inventory/budget writers recheck occupancy, allocations and composition
  at write time, including replay after a partial failure. A precommit check alone
  did not protect recovery after a player moved an item or changed selections.
- Inventory checkpoints restore capacity before native saved-item enumeration,
  track own scalar contributions across reused avatars, and preserve native item
  layout before safe unload. Unsafe unload retains guards and reports the reason.
- Talent checkpoints preserve managed saved allocations independently of the
  future preset. Restore occurs once per avatar/run-save identity; later native
  menu reloads, first-departure events and addon hook reloads cannot resurrect a
  stale budget over an explicit decrease.
- Starting-grant checkpoints distinguish saved zero from absence, preserve
  spending/reconnect balances, and reject duplicate or uncertain grant replay.
  Fresh native saves initially lack SaveVersion; stable per-player checkpoints
  avoid sharing the host's legacy fallback balance with later guests. Native
  SaveVersion migration does not change addon checkpoint ownership.
- Cumulative offsets from native totals larger than the configurable maximum
  remain savable (for example native 120 slots reduced by 97 to 23). Reset can
  remove a fruit contribution even with a zero native amplifier when safe.

Final automated verification passed:

| Verification | Result |
| --- | --- |
| Portable parsers, plans, presets, synchronization and existing feature checks | 918 checks |
| Actual command/session services with data fixtures | 417 checks |
| Actual starting-resource hooks with lifecycle fixtures | 36 checks |
| Actual budget/inventory hooks with controlled native patch fixtures | 62 checks |
| Debug and Release solution builds | Zero warnings/errors, `DeployMod=false` |
| Installed-game IL compatibility | Existing names/panel/Fountain/choices plus new grant, inventory, talent, fruit and SyncVar/RPC contracts passed |

The 1,433 automated checks do not simulate real Unity execution, Mirror transport,
or another player's renderer. Fixtures use the packaged Harmony API; the reported
HarmonyX helper regression has a separate binary-reference assertion. Final
independent review found no additional critical defects in the reviewed code.

Manual follow-up on the built version: host plus an unmodified guest, character
and costume changes, native presets/Root's Retreat, two consecutive run restarts,
town and run reconnect, save/resume with occupied expanded slots and allocated
talents, and guest menus open while decreasing budgets. Check current balances
against future starting previews, and verify new-game/fresh late-join allowances
once each. Initial lobby generation may still precede the new avatar's dice
allowance; already-created offers are not regenerated. No deployment was run.

## Repeated re-entry follow-up (0.15.1)

The [full-quit/reconnect review](session-reentry-review.md) reproduced and fixed
stale inventory/talent checkpoint acknowledgment, missing offline reset intent,
and saved native gains being reclassified as addon ownership. Other families
already used fresh avatar lifetimes and were left unchanged. Unsafe decreases
retain saved items/allocations and keep combined inheritance pending until safe.

Verification on 2026-09-24: 938 portable checks, 489 runtime checks (including
actual checkpoint/runtime composition), 57 starting-hook checks and 62 native
budget/inventory hook checks passed: **1,546 total**. Debug and Release builds
passed with zero warnings/errors and `DeployMod=false`. Installed-game IL
compatibility checks passed, including existing name/panel/Fountain/choice paths,
resource boundaries and the embedded runtime. Independent review found the
native-gain ownership issue; its regression failed before the fix and passed
afterward. Final rereview found no additional actionable defect.

Live Unity/Mirror quit/reconnect remains a manual check. Native authentication can
assign a new GUID to an overlapping town reconnect, and native town menu drafts
are not all disconnect-persistent; see the review's limits. No deployment ran.
