# Independent PvP DoT intervals

Added in `0.59.0`, 2026-10-16. Burn, Plasma Burn, Frostbite and Poison each have
their own **0.75-second** default interval while PvP balancing is enabled.
Configure them under **Combat → DoT intervals** (`전투 → 지속 피해 간격`), or:

```text
/one friendlyfire dot burn 0.75
/one friendlyfire dot plasma-burn 0.75
/one friendlyfire dot frostbite 0.75
/one friendlyfire dot poison 0.75
/one friendlyfire dot burn reset
/one save
```

Each accepts **0.05..10 seconds**, up to two decimals. Per-effect `reset` restores
only that effect to 0.75; full friendly-fire reset restores all four. Controls
edit the existing shared balancing policy: effective friendly fire and balancing
must be active for the intervals to apply. An edit takes effect on existing
debuffs without stopping casual combat, Deathmatch, Tournament or Play Tag, or
resetting scores, bomb state, participants or damage budgets.

These are actual configured periodic intervals, subject to frame granularity,
not multipliers of native tick speed. BURNSPEED cannot shorten Burn/Plasma Burn's
PvP interval. PvE and disabled balancing resume native timing and speed bonuses
immediately. Each editor has an independent draft/Apply; the compact 2x2 layout
retains the existing window size and shared participant/KDA/revive/master actions.
Refresh formats only values that change. EN/KO labels and command/status output
include all four settings. Nondefault values are saved in preset v28 as
`friendlyfire dot-<effect> N`; presets v1..27 inherit 0.75 for every effect.

## Native boundaries

The former implementation was a **0.75 elapsed-time multiplier**, not a fixed
0.25-second DoT interval. Burn/Plasma constructor defaults then yielded about
0.667s before speed bonuses, while Frostbite/Poison yielded about 1.333s. The new
defaults replace those four timings. Wound retains its old multiplier. The
separate 0.25s Shock/Freeze damage gates and Dark Cloud strike gate are unchanged.
Plasma's shock accumulator, refresh/expiry attacks and damage budget also remain
unchanged. This does not enable Frostbite damage on a build without the native
`FROSTBITEDAMAGE` condition, nor change stacks, movement slow, Freeze stun or expiry.
See [the other balancing layers](pvp-balancing.md).

`FriendlyFireDebuffBalanceHooks` wraps only each audited periodic `Timer.Update`
call, passing the native timer, native delta, effect instance and explicit kind.
`UpdateDebuffTick` checks current server authority, balancing and shared permitted
player pairs, then advances the same timer by
`Time.deltaTime * timer.time / configuredSeconds`. Native tick length, elapsed
storage, reset-on-tick and active-once behavior remain owned by `Timer.Update`.
Partial interval progress survives edits: halfway through a tick remains halfway
through after a new interval is selected. Native reset-on-tick means a stalled
frame produces at most one tick, without a catch-up burst.

No prefab/timer configuration/stat writes, per-effect dictionary, actor cache,
extra update pass or custom RPC is introduced. Native damage and FX replication
serve stock guests. PvE, self hits, system poison, missing/non-player owners and
nonparticipating players retain native timing; existing hostility admission still
blocks disallowed damage. Companion effects resolve through owner participation.
The shared debuff receipts retire effects from departed/replaced connections
before they tick. Rejoins and run changes read current policy rather than saved
per-connection settings. Unloading removes the timer hooks with the shared combat
feature, restoring native cadence.

## Verification

Release build: zero warnings/errors with deployment disabled. Passed 3,877 combat
checks, 2,254 runtime checks plus 69 Bat lifecycle checks, 343 interval policy/
preset checks, 3,557 catalog checks, installed-game contracts and 32 compiled
Harmony references against co-installed HarmonyX 2.9. Independent read-only
review found no actionable issues.

Executable tests cover default/independent intervals, changed native timer
lengths, high BURNSPEED, live partial-progress edits, disable/unload, PvE/system/
ownership exclusions, mode edits, restart/rejoin and preset migration. All four
warmed timer paths allocate zero bytes across 1,000 fixture updates. Installed
contracts check timer fields, call boundaries, effect-kind mapping, unchanged
native charge/stun/expiry instructions and rejection of drifted IL. Assembly
SHA-256: `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.

Live host/stock-guest timing and rendered UI remain unverified. Check each effect
independently, edit during an existing effect, disable balancing to compare native
timing, and repeat after a run/reconnect. Nothing deployed; no game launch.
