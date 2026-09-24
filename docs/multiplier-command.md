# Native-baseline multipliers

Added in `0.14.0` for host-only stats and Wishing Fountain commands. Current and
joining players receive native synchronized changes; guests need no addon.

```text
/stats luck x3
/stats attackspeed x2
/stats critical x1.5
/fountain x2
/mod status
/mod save
```

`xN` targets **each player's current native value times N**, excluding this
addon's existing contribution. Native luck 10 and 20 becomes 30 and 60 with
`x3`. Repeating `x3` does not compound; `x2` followed by `x3` still means native
times 3. The factor replaces a previous Set, offset or multiplier setting.

Native includes the character's current equipment, passives and buffs. The
retained factor follows those changes through shared reconciliation. Display
units apply: native attack speed 100% becomes 200% with `x2`; its internal raw
zero is not the value being multiplied. A zero native stat remains zero until
native sources increase it.

## Syntax and modes

- Use `xN` or uppercase `XN` (`X3`). `set x3` is also accepted.
  In the host panel, enter `x3` and click **Set** on Stats or Fountain.
- Factors accept a decimal point and at most two decimal places, from 0 to
  10000. Resulting values must also meet the feature's existing bounds.
- Integer metrics require an exact integer result. Native luck 3 with `x1.5`
  is rejected; luck 10 becomes 15. No rounding or clamping is introduced.
- `x1` restores the exact current native baseline and removes retained intent,
  like the selected stat's reset or Fountain reset. A fault involving the whole
  stats family still requires `/stats reset`, as with a selective stat reset.
- `x0` retains a zero multiplier, where the stat's bounds permit zero. It is
  different from resetting. Reset later restores native changes made meanwhile.
- Add/Subtract after multiplier mode starts a fresh native-relative offset.
  With native luck 10: `x3`, then `+5`, then `+2` gives 30, 15, then 17.
  `add x3` and `sub x3` are rejected because their operations conflict.
- Older non-multiplier commands keep their behavior. In particular, Fountain
  `set 100`, then `+5` still gives 105; character stats switch to native +5.

`/choices` still controls **extra** candidates, whose native baseline is normally
zero. Total offers vary by generator, so multiplier input is rejected with an
explanation. It does not multiply current addon extras or redefine the command
as a total count. The five [investigated resource controls](resource-settings-investigation.md)
are not implemented by this change.

## Synchronization, storage and reset

Commands validate all ready players before writing. An incompatible result for
one player rejects the entire command and leaves retained intent unchanged.
The host maintains factors when native inputs change and flushes them through
existing native-read boundaries. Fountain maintenance runs before carryover-cap
reconciliation, covering immediate run entry and restarts on reused avatars.

If a later native change makes a target out of range or unrepresentable, the
addon removes its contribution when the baseline can be safely restored and
marks the setting suspended. It retries when inputs change. `/mod status` and
the panel show retained factors, current values and synchronization outcomes.
They do not apply settings while being read. Partial writes retain the existing
journal and block further mutations until explicit family reset recovers them.

`/mod save` stores **factors**, not each player's calculated totals. Multiplier
presets use `SephiriaOne preset v2` with `multiplier N` rows. Existing v1 presets
remain supported; saving only older setting types still produces v1. Older
addon versions cannot read v2. Reset affects this session only: save again or
use `/mod forget` to change future-session loading.

Native guest UI caches remain unchanged; reopen the Fountain or affected native
panel if needed. Independent overwrites of the same native field cannot be
reconstructed from contribution markers, matching existing additive settings.

## Verification

On 2026-09-24, 750 portable checks (85 multiplier-specific) and 354 runtime
fixture checks passed. Debug/Release builds passed with zero warnings/errors
and deployment disabled. Installed-game lifecycle, Fountain boundary, name,
panel, candidate guards and embedded dependency/license checks passed. An
independent code review reported no actionable findings. A subsequent final
comparison caught and restored the legacy rejection of negative joining
Fountain baselines, with a failing-then-passing regression check.

Portable checks cover distinct baselines, replacing rather than stacking,
display offsets, exact fractions, native amplification, all-player rejection,
zero/identity, transitions, invariant parsing and versioned preset round trips.
Runtime fixtures cover shared chat/panel dispatch, native changes, late arrivals,
repeated restarts, read-only status, save/reload, suspension/recovery and partial
native writes. See [design and implementation plan](multiplier-command-design.md).

Live host/unmodified-guest verification still required:

1. With unequal native luck/Fountain values, apply `x3`, repeat it, then `x2`.
   Check each peer's values and Fountain items after immediate run entry.
2. Change native loadout, costume, talents and equipment. Check the factor and
   Fountain carryover on second/third runs, including a late join and reconnect.
3. Check `x1`, `x0` where allowed, fractional rejection and Add after multiplier.
4. Save, restart the game and host again with different characters. Verify
   inherited factors, reset/save distinction and native panel reopening.

No deployment is part of this change.
