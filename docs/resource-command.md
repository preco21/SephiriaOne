# Resource controls

Version 0.15.0 adds the Resources page in `/one ui` and the host-only
`/resources` command. All five controls use the game's existing server state,
SyncVars and RPCs; guests do not need the addon. Live peer rendering remains a
manual verification step.

```text
/resources dice +5
/resources slots +6
/resources talents set 20
/resources fruit x2
/resources leaves set 1000
/one status
/one save
```

| Name | Set means | Configurable total |
| --- | --- | --- |
| `dice` | Initial reroll dice on a future fresh start | 0–1,000 |
| `slots` | Main inventory grid slots, excluding potion belt and side bag | 6–96 |
| `talents` | Total talent budget, including allocated points | 0–1,000 |
| `fruit` | Total fruit-skewer budget, including adaptive selection | 0–100 |
| `leaves` | Future starting leaf allowance | 0–1,000,000,000 |

These are addon validation bounds, not native hard limits or guarantees about
every extreme layout. A result must be an exact whole number for every player.
Large inventory layouts require in-game visual checking.

## Arithmetic and reset

`+N`, `-N`, `add N`, and `sub N` accumulate an offset from each character's own
native baseline. `xN` replaces the adjustment with native baseline × N. Repeating
`x3` does not compound it. A delta after Set or xN starts a new relative offset.
Use `set xN` with the panel's Set button. Fractional factors require every
resulting total to be representable as a whole number.

`/resources slots reset` restores the selected native setting;
`/resources reset` resets all five resource policies. `x1` is a selected reset.
Reset changes the active session; `/one save` explicitly updates the saved copy.
`/one forget` deletes only the future-session preset. Existing `/fountain`,
`/stats`, and `/choices` commands keep their behavior.

For slots and point budgets, Set establishes an addon contribution once per
player. Later native gains remain intact. Offsets and factors follow native
capacity/stat changes. Impossible maintenance is suspended with its existing
contribution retained, rather than discarding items or allocated points.

## Starting grants and saved runs

Dice/leaf commands never refill or subtract from current spendable balances.
Dice policy is fixed when a fresh initialization grant begins. Starting leaves
can be changed in the lobby until the first departure: as of **0.15.4**, that
pending grant reads the latest host setting, including reset. Save the settings
before exiting to retain them for future hosted sessions.

Leaves have a tree-shop seed followed by the native `STARTINGMONEY` departure
grant. The addon grants at most the normal seed initially; any additional amount
arrives at departure. This permits decreases without confiscating intervening
earnings when configured before initialization. A late decrease cannot reclaim
the seed already granted. If the new target is below that paid seed, or later
native inputs make the requested total invalid, it restores
the withheld native portion and uses the native allowance, with a warning.
Spending and earnings between these two grants remain intact. Brand-new arrivals
already inside a dungeon have only their native initialization seed as baseline.

For example, native seed 200 plus departure bonus 100 has an allowance of 300.
Setting `leaves x3` in the lobby makes the total allowance 900: departure adds
700 after the 200 already granted. Spending 50 and earning 20 in town leaves
870 after departure. Repeating the factor or moving to another floor does not
grant that amount again. A reconnect before first departure restores its spent
balance and uses the current host intent for the still-unpaid remainder. A saved
pending allocation remains the fallback if a new host scope has no leaves intent.

Saved zero dice/leaves means spent resources. Restoring a saved run or reconnecting
restores the saved balance, without multiplying it or granting it again. Separate
run checkpoints preserve applied ownership and once-only grant state. Initial
dice also update the native maximum for that run. Lobby generation that occurs
before avatar initialization can still inspect the prior native maximum; the
addon does not retroactively regenerate already-created offers.

Inventory capacity is restored before saved item positions are read. Saved talent
budgets are restored before saved allocations can be clamped. These run checkpoints
are separate from future presets, so forgetting a preset does not truncate a
saved inventory or managed talent allocation. A completely fresh run without a
saved preset/checkpoint uses native settings. No addon code rewrites guest profile
selections. Native selection menus retain their ordinary save behavior.

## Safe changes and diagnostics

All participants are validated before command writes begin. Decreases reject
occupied trailing slots, mystic positions, engravings, allocated talent points,
committed fruit selections, and relevant pending guest menus. Clear or move native
selections/items first, then retry. Fruit changes affect the selectable budget;
they do not replay bonuses already consumed for the current run.

Native writes use the shared journal. Partial writes stop maintenance and other
commands; `/resources reset` can recover only within the same valid player/run
identity. It is deliberately refused after an identity/run change. Inspect
`/one status` and Player.log if recovery is no longer safe, then leave the session.

Compatibility guards are independent. A failed starting-resource boundary does
not suppress working slot or point-budget controls. The panel/status reports the
underlying failure. The reported HarmonyX `LoadsConstant(string)` incompatibility
was replaced with direct IL checks. Do not overwrite another addon's Harmony DLL.

No new hotkey was introduced. Open `/one ui` or use the existing pause-menu button.
