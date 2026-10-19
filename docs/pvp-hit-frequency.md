# Optional PvP hit-frequency scaling

Added in `0.60.0`, 2026-10-16. Default **off**, host-controlled, using the existing
damage path for unmodified guests. Requires friendly fire **and** PvP balancing.
The user's selected starting threshold is **4 hits per second per opponent**.

## Controls

In **Combat → PvP balancing**, enable **Hit-frequency scaling** (`연타 피해 보정`).
The checkbox uses the same host command as chat:

```text
/one friendlyfire hit-frequency on
/one friendlyfire hit-frequency off
/one friendlyfire hit-frequency reset
/one friendlyfire status
/one save
```

`reset` switches only this preference off. The preference can be configured while
balancing is off, and takes effect when its prerequisites are enabled. Toggle
edits preserve the current match, K/D/A, damage-budget credit, refill rate, guard
cost and DoT intervals. Repeating `on` does not reset history. Save stores only
the preference, never runtime hit history. Enabled settings use preset v29;
earlier schemas inherit off, and off adds no new row or version requirement.

## Damage model

The existing native defenses and ordinary friendly-fire damage percentage still
run first. The sequence within PvP balancing is:

1. Existing per-hit damage softening.
2. Existing Shock/Freeze damage-pulse admission.
3. Optional frequency multiplier: `min(1, sqrt(4 / recentHitCount))`.
4. Existing per-opponent damage budget (20% maximum-HP burst, configured refill).
5. Current role incoming modifier, positive damage minimum (since `0.67.1`),
   native shields/HP and unused-budget refund.

| Recent hits against this opponent | Per-hit frequency multiplier |
| --- | --- |
| 1–4 | 100% |
| 8 | about 71% |
| 16 | 50% |
| 32 | about 35% |

The first hits retain their strength. Further rapid hits progressively lose
damage without changing attack speed or discarding native effects/callbacks.
The budget is charged only for the frequency-scaled allowance. When the budget
is already the tighter limit, enabling frequency scaling may not further reduce
the actual damage of every hit; neither mechanism can increase the other's
allowance. Since `0.67.1`, the [positive-hit minimum](pvp-balancing.md#minimum-positive-damage-0671-2026-10-20)
is charged in full, with fractional debt carried until refill. Sub-one frequency
results therefore deal one before shields when admitted, but cannot bypass an
exhausted budget. This is diminishing returns, not a promise of equal DPS for all weapons.

Count positive resolved hit attempts reaching this stage, including those later
limited to zero by the budget. This prevents exhausted-budget barrages from
appearing slow. Misses, invulnerability, native guard/parry, invalid or zero
resolved damage, and rejected Shock/Freeze pulses do not add hits. Shield
absorption and later native reductions retain their existing accounting; they
do not roll back the hit count. The same attacker shares history across weapons,
spells, DoT, secondary artifacts and owned companions. Reflections use the reverse
directed pair. Other attackers and victims remain independent.

This does not change native player stats, proc chances, status applications,
crowd control, attack cadence or pooled `DamageInstance` values. Existing Burn
lifetime, DoT intervals and Dark Cloud cooldown remain separate restrictions.
PvE, self hits, non-player victims and disallowed participants bypass this scaler.

## Implementation and lifetimes

`FriendlyFireHitFrequency` stores eight counters at 125 ms resolution. A rolling
sum avoids scanning prior hits; time advancement touches at most eight buckets.
This approximates the last second: a hit ages out after 0.875–1 seconds depending
on its position within a bucket. It has no polling or per-hit collection growth.
The eight-counter array is allocated on the first eligible hit in a pair's
lifetime; warmed hits allocate nothing. Counts saturate safely, invalid clocks
do not modify history, and clock rollback clears stale history.

The struct lives in `FriendlyFireRuntime.BalanceWindow`, using the same weak
connection receipts, score/participant/balance epochs, attacker/victim life
generations and floor identities as the damage budget. Native revival, K/D/A
reset, participant changes, reconnects, floor transitions and mode cleanup
therefore cannot inherit an old pair's hit-rate penalty. Match start/end and
selection continue through the existing shared runtime for casual combat,
Deathmatch, Tournament and Play Tag.

`SessionSettings.CombatFrequencyRevision` advances only when this preference
changes or full combat reset occurs. A pair lazily discards its old frequency
history on the next enabled hit. This revision is deliberately separate from
`CombatBalanceRevision`: toggling frequency must not refill the damage budget,
clear Shock/Freeze/Dark Cloud clocks, reset K/D/A or interrupt a match. Settings
application is centralized so sibling edits and temporary mode overlays retain
the preference.

All policy, hit counts, scaling and accounting run on the host. Native damage,
shield and death replication continues unchanged; no guest addon, asset or new
network message is required by this implementation.

## Verification and limits

Command/preset tests cover default-off behavior, strict parsing, older schemas,
atomic malformed-preset rejection, sibling-setting preservation and mode overlays.
Executable Harmony fixtures cover the 4/8/16-hit rates, burst/idle/clock behavior,
budget composition, no token refill on frequency toggles, shields, guard,
reflection, nested procs, companions, DoT, authority/participant exclusions,
respawn/reconnection/floor/score cleanup, and zero warmed per-hit allocation.
Session tests cover live edits in every combat mode, saved settings and restarts.
Compiled integration checks verify UI/command wiring and frequency-before-budget
ordering. English/Korean catalogs pass key/placeholder checks.

Release build passed with zero warnings/errors; 3,995 executable combat checks,
2,266 session checks plus 69 Bat lifecycle checks, 83 frequency setting checks,
the full policy/catalog/installed-game suites, and the 33-reference Harmony 2.9
API audit passed. Independent review found no actionable issues.

These are automated fixtures and installed-assembly checks. Live Unity UI layout
and host/stock-guest gameplay still need testing. Nothing was deployed or launched.
