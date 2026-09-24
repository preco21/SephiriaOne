# Resource and capacity settings investigation

Investigated 2026-09-24 against addon `0.13.0` and installed Sephiria `1.0.33`.
This is a feasibility report and proposed design. No runtime feature was added,
game executed, save changed, addon build run or addon deployed.

## Conclusion and scope

All five requested settings have native paths that can affect unmodified guests
when the host installs the addon. They should share commands, presets, status,
validation and the control panel, while distinguishing **starting grants** from
**capacity/budget changes**. Continuously enforcing a starting wallet/dice target
would refill resources after spending and is not the intended feature.

| Requested setting | Native mechanism | Proposed meaning of Set | Main complication |
| --- | --- | --- | --- |
| Initial reroll dice | `PlayerAvatar.maxRerollDice`; initialization of `NetworkrerollDice` | Total initial dice allowance for a fresh player/run | Saved current dice override the default; native maximum-dice effects can also change current dice. |
| Inventory slots | `GridInventory.CurrentInventoryStorage`, `AddStorage(short)` | Total main-grid slots, excluding potion belt and side bag | Shrinking occupied slots can move/drop items; capacity must exist before saved items are enumerated. |
| Talent points | `PlayerAvatar.NetworkmaxPassivePoint` and `passiveStats` | Total talent budget, including already spent points | Saved allocations are clamped before ordinary player readiness. |
| Fruit-skewer points | Native `fruitSkewerDefaultCount` plus effective `FRUITCOUNT` | Total selectable fruit-piece budget, including adaptive selection | Owner menus/profile choices are separate from the server budget and run-consumed bonuses. |
| Initial leaves | Tree-shop starting grant plus the separate `STARTINGMONEY` departure grant | Total starting allowance, rather than only one source | Two grant phases, possible spending between them, and late joiners missing the departure loop. |

Working assumption for the proposal: initial dice/leaves affect fresh starts,
without immediately changing an existing run's spendable balances. The user was
asked whether to also make an immediate adjustment; that preference remains open.
Inventory means the main item grid. Talent/fruit Set targets are proposed **total
budgets**, not additional unspent points. Show total, allocated and available
separately to avoid ambiguity.

## Initial dice

`PlayerSpawner.Initialize` restores `PlayerRerollDice` or
`Player{slot}RerollDice`; only an absent key falls back to `maxRerollDice`.
A saved zero means spent dice, not an uninitialized player.

Maximum and current dice are native SyncVars. Current-dice changes invoke
`HookRerollDiceValueChanged`, consumed by the dice indicator and several reward
menus. Unmodified guests therefore already have the necessary transport and UI
consumers. Maximum dice also influences native stage/reward generation; changing
it is not merely changing a label.

Do not implement this as a guessed `INITREROLL` custom stat. No such consumer was
found in the assembly-wide scan. Native maximum-dice status effects call
`AddMaxDice` and can also add/remove current dice; using them indiscriminately
would change already-spendable resources.

Track desired initial allowance independently, reconcile its owned maximum-dice
contribution at appropriate boundaries, and apply the starting grant only when
the native player/resource has no saved current balance. Reset should remove the
initial-setting policy without refunding or reclaiming consumed dice.

## Inventory slots

`GridInventory.AddStorage(short)` follows the existing server path, writes the
native synchronized capacity, and emits both storage and height RPCs. The owning
client's `UI_CharacterStatusPanel.OnInventoryStorageChanged` rebuilds its grid
from the new size. A raw SyncVar assignment alone omits those notifications.
The other-player inspection panel creates its grid on opening and has its own
cache timing; the addon should not promise immediate refresh of every stock UI.

Native slot sources include inventory status effects, costume/passive effects,
farm abilities, level bonuses, and purchased/found inventory expansions. Track
only the addon's contribution so reset preserves those native changes. A normal
capacity policy should not undo later legitimate slot rewards.

For a reduction or reset, preflight every player's target and every removed grid
position. Reject the entire command if any removed position contains an item.
Checking only the number of items is insufficient: an item can occupy the last
slot in a mostly empty bag. Do not call the native negative resize first and try
to undo its automatic relocation/drop side effects afterward. Include grid
metadata, fixed engravings and pending item interactions in the compatibility
checks before choosing final supported limits.

The field is a `short`, the default width is six, and the class declares
`MaxWidth=6` / `MaxHeight=7`; however, `LocalAddStorage` does not enforce a 42-slot
cap and the main UI allocates rows dynamically. Neither 42 nor `short.MaxValue`
has been established as the supported addon limit. Large grids need bounded
validation against signed coordinates, reserved potion rows, layout and consumers.
Keep width unchanged and begin live validation near normal inventory sizes.

### Saved-run hazard

Native saves write item positions, while `PlayerSpawner.Initialize` reloads only
positions below the capacity that exists at that moment. Native orphaned
`INVENTORY_SLOT` effects are restored earlier. A bare addon `AddStorage` write
does not become one of those saved effects.

Applying the addon capacity in `LateUpdate` or at an Initialize postfix can thus
skip items saved in added slots. This feature needs explicit run-checkpoint
ownership/capacity restoration **before** native inventory enumeration. A new
or reduced saved preset must not shrink a resumed occupied inventory before its
items are recovered. Keep the previous checkpoint capacity until a safe
reduction is possible. Do not attribute indistinguishable native inventory
status effects to this addon or change the user's original rewards.

## Talent points

The native budget is `maxPassivePoint`; used points are the sum of `passiveStats`.
The remaining number shown by the talent menu is their difference. The cap has
native server-to-client synchronization, and native purchase commands validate
against it, so guests can spend an increased budget without installing the mod.
Individual talents retain their native `PassiveEntity.maxLevel` limits.
Native allocation/reset remains restricted to town, and unlocks remain native.
Increasing the budget during a dungeon run does not select talents for players
or enable the town allocation menu there.

Before decreasing or resetting the budget, reject a target below the player's
committed allocation. Do not clear selected talents automatically. Changes made
through the native talent UI can be saved in each player's local profile, so
resetting an addon budget is not a rollback of that profile.
Reconnecting without the increased budget can clamp those saved allocations;
a later native save may persist the clamp. Never call a guest avatar's
`SavePassiveStat` on the host, where it would address the host's local profile.

`LoadPassiveStatOnServer` passes saved selections to `AddPassiveStatOnServer`,
which clamps each allocation to the available cap. Initial native setup loads
tree-shop data and hard-mode rewards before these selections, then initializes
the player. Apply the retained budget **before this clamp**, using an explicitly
scoped initialization adapter; normal ready-player reconciliation is too late.
Also guard later native allocation commands and account for changed native caps
from progression, including Root's Retreat rewards.

The native talent menu refreshes on opening and allocation callbacks, rather
than a cap-change hook. Guests may need to reopen it after a host budget edit.
Do not resurrect the removed client-only presentation patches to conceal that
native behavior.

## Fruit-skewer points

The inspected native default is six, with the per-player effective `FRUITCOUNT`
added to it. A point represents one selected positive or negative fruit piece;
adaptive selection consumes one point too. It is not a drop-weight percentage
or merely a count of distinct categories. Native per-category selection limits
remain separate from the total budget.

Adjust the server-owned `PlayerAvatar` custom stat using tracked contribution
and multiplier-aware planning. Set should target the total displayed budget,
including the native default. Preserve equipment/passive contributions; detect
incompatible multipliers instead of silently rounding a requested total.

`PlayerLocalDataStorage.fruitSkewerBonus` contains the owner's chosen composition
and is synchronized from the client. The native menu writes that composition to
the owner's profile. Change the budget, not this list or the guest's profile.
Reductions should reject a target below committed selection cost. Pending native
menu edits may not yet be visible on the host; use the native preparing-state
signal conservatively for reductions and verify in-flight submission races.
These flags are network observations, not an atomic acknowledgment of every
remote draft. Revalidate committed selections before writing, and do not promise
that the host can inspect unsaved guest choices. Native profile loading can
retain an over-budget composition, and closing the menu saves it before warning
about excess; the native UI does not automatically trim it.

At first dungeon entry, `DungeonManager.LoadStageAndMove` reads the allowance
and grants the selected drop-weight effects. Reconcile the budget before that
read. After consumption, raising the selection budget does not automatically
rebuild this run's consumed skewer bonuses. Replaying the native start routine
would duplicate other grants. Keep budget configuration and consumed-run effects
separate, and explain when a change applies.

The inspected consumption loop applies an entry before checking whether the
allowance has been exhausted. With a nonempty saved composition and zero or
negative remaining allowance, it still grants the first piece. Reject unsafe
reductions, including adaptive-slot edge cases, unless that native boundary is
deliberately corrected and tested. A numeric zero alone is not proof of zero
consumed effects.

The native fruit menu caches its counter/button state until selection changes
or reopening. No immediate remote-menu refresh is promised. Reconnect/resume
restores saved consumed pieces separately from current selections. A genuinely
new player arriving after the run starts misses the first-entry grant loop:
inheriting the budget alone does not grant consumed skewer benefits mid-run.
Scope those benefits to the next eligible run, or specify and test a separate
once-only newcomer grant. Do not treat a reconnect as a fresh grant.

## Initial leaves

There are two sources, both relevant to an exact total:

1. `PlayerSpawner.Initialize` adds the saved wallet when its per-player key
   exists, otherwise `TreeShopItemStorage.GettStartingMoney()`.
2. On first dungeon departure, `DungeonManager.LoadStageAndMove` adds a positive
   effective `STARTINGMONEY` value from the character's native stats.

The wallet uses the native `NetworkcurrentMoney` SyncVar and money-indicator
hook. These APIs do not protect the addon from integer overflow or invalid
negative results; use checked wide arithmetic and all-player validation.

Changing only `STARTINGMONEY` would implement a departure bonus, not total
starting money. Setting the wallet at departure would erase intervening income
or spending. Recommend transforming only the identified starting grants, with a
record of which phases have been consumed. Never globally intercept every
`AddMoney` call or continuously force the current wallet to a target.

For example, an absolute allowance `T` could allocate
`min(nativeTreeSeed, T)` at the seed phase and the remainder at departure.
This is a proposed allocation rule, not a native behavior or settled preference.
Signed additions/reductions also need an explicit phase-allocation rule. A fresh
late joiner already inside the dungeon needs a once-only settlement for any
allowance whose departure phase was missed; a reconnect with saved money must
not receive it again. Special races without normal departure need explicit
handling. Keep these distinctions visible in the eventual command help.

## Shared design recommendation

| Approach | Assessment |
| --- | --- |
| Add all five directly to the existing numeric stat catalog | Too narrow: storage and talent caps are not ordinary custom stats, and starting resources are consumable grants. |
| Implement five independent controllers and UI handlers | Possible, but repeats the event/readiness/once-only problems already addressed by the shared sync system. |
| Shared resource definitions with typed native adapters and phase rules | Recommended. Reuses the current infrastructure while making each resource's timing and constraints explicit. |

Suggested command family, **not implemented**:

```text
/resources dice set 20
/resources slots add 6
/resources talents set 100
/resources fruit add 2
/resources leaves set 500
/resources <name> reset
```

Expose Set/Add/Subtract/Reset through `SettingsActions` and the same host panel.
Use one definition list for command names, units, display labels, supported
operations and timing. Reuse existing planners where applicable rather than
copying command or UI arithmetic. Proposed relative semantics: accumulate an
offset from each player's native allowance, and a delta after Set starts relative
mode, matching the user's chosen stat semantics. Confirm this alongside the
initial-resource timing decision before implementation.

Reuse `ReconciliationCoordinator`, `StateWriteBatch`, readback, fault containment,
scope identity and immutable settings snapshots. Add explicit phases for
pre-talent-load, pre-inventory-restore, fresh-resource initialization and first
departure. Existing `HostStateAdapter.IsReady` deliberately requires state that
does not exist yet at some of these boundaries; do not weaken it for ordinary
commands. Introduce a narrow, audited readiness contract for initialization.

Keep these three kinds of state separate:

- **Desired policy:** retained Set/offset intent and the explicit future-session
  preset. Extend the versioned codec while retaining a reader for existing v1
  presets; new fields must not silently drop old families.
- **Observed native state:** live capacity/budget, spent/available values, current
  consumables, native baseline and actionable/unavailable reasons.
- **Run checkpoint/grant state:** resource-specific native save-key presence,
  phase completion and owned capacity needed to resume safely. This cannot be
  inferred solely from the user's future-session preset.

Key entitlement by native run/player-save identity and phase, while using object
lifetime to prevent writes through stale references. Run-generation events
invalidate observations; they do not authorize replay of a grant. The official
session-start event occurs before restart player initialization and after some
stage consumers, so it cannot serve as the sole application hook.

Reset should remove tracked addon intent/contributions while preserving native
progression. Already-spent starting resources are not refunded or reclaimed.
Occupied capacity and allocated budgets need safe rejection/defer rules. Native
write batches remain journaled rather than falsely advertised as atomic rollback
of item movement, profile saves or client-side effects.

## Required verification before shipping

- Unequal character baselines, repeated relative changes, Set-to-relative,
  native costuming/passives/Root's Retreat changes, zero, negative and overflow.
- First run and repeated runs on reused avatars; native menu changes followed
  by same-frame start; grants before normal ready-player enrollment.
- New arrival versus reconnect; saved session plus a genuinely new player;
  exact saved zero versus absent per-resource key; reused network/save slots.
- Save/resume with items in added slots, changed/forgotten future presets,
  native slot rewards, occupied shrink/reset and partial-write recovery.
- Saved talent allocations above the native cap; individual talent limits;
  live guest selection while a host edits/reduces the budget.
- Fruit positive/negative pieces, adaptive cost, committed versus dirty menu
  selections, zero allowance with saved pieces, per-category limits, consumed
  effects, fresh mid-run arrivals versus reconnects, and guest UI reopening.
- Leaves earned/spent between seed/departure; duplicate/missing departure,
  special races, mid-run newcomers, reconnects and preset reload without regrant.
- Installed native signatures/serialization/RPC and grant-boundary IL checks;
  then live host plus unmodified guest tests for actual delivery and UI behavior.

## Evidence and limits

Primary evidence is the installed game assembly, SHA-256:

```text
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
```

Inspected paths include `PlayerSpawner.Initialize`, `SaveCurrentSessionData`,
`RestartNewGame`, `CmdSetDefaultPlayerData`; `HorayNetworkManager.NewGame`,
restart and disconnect/save paths; `DungeonManager.LoadStageAndMove`;
`GridInventory.LocalAddStorage` and generated RPC/serialization;
`PlayerAvatar` dice/passive methods and generated setters;
`TreeShopItemStorage`; `UnitAvatar` money/stat arithmetic; the talent, fruit,
inventory, dice and money native UI consumers and relevant serialized assets.
Fresh inventory/player/UI decompilations matched the earlier cached code.
The official API page remained inaccessible through the web tool; no external
mod description was used as proof of native correctness.

Decompiled source, IL cross-reference tools and asset excerpts remain outside
the repository under the temporary research directories. No live transport,
rendering, large-inventory compatibility or save/resume implementation was tested.
The currently implemented version remains `0.13.0`.

See [shared synchronization](synchronization-guide.md),
[host control panel](control-panel.md), and
[host-only compatibility](presentation-compatibility.md).
