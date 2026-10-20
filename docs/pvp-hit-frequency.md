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

## Low-scale fairness audit (2026-10-20, 0.73.0)

This is an investigation and recommendation, not a gameplay change. The user
reported 0–4 displayed damage and suspected skipped rapid hits at `0.05%`, tested
before the minimum-one patch. The current percentage means multiplication by
`0.0005`, not `0.05`.

The actual sequence remains: native resolved damage → configured percentage →
per-hit softening → debuff schedule admission → optional hit-frequency factor →
directed-pair budget → incoming-role multiplier → positive minimum → native
shields/MP/super armor/HP. Hit-frequency scaling still defaults off and requires
the independent PvP-balancing toggle.

### What can look like a skipped hit

- The square-root frequency factor does not schedule, merge or drop ordinary
  valid hits by itself. Counts use eight 125 ms buckets, so the factor changes
  in steps and is an approximation rather than an exact moving-second rate.
- The damage budget **does** clip a hit to remaining credit, including zero.
  The lost part is discarded, not queued or repaid as later damage. Refill
  replenishes capacity for future hits only. Default capacity is 20% max HP and
  refill is 70% max HP/sec; role modifiers may adjust refill/incoming damage.
- Debuff schedule protection discards damage pulses before the configured next
  eligible time. Dark Cloud's separate cooldown skips the whole excess strike,
  including its strike callback. Neither has a catch-up queue.
- Native evasion, guard/parry, protection, revival invulnerability, shields and
  super armor remain additional causes of no HP loss. The friendly-fire nested
  proc guard rejects unrecognized recursive damage chains; it is not a
  frequency threshold, but a missing artifact allowance could reject legitimate
  secondary hits. A specific dropped hit needs a trace to distinguish these.
- Native HP is floating point. In the inspected `UnitAvatar.ApplyDamage`, HP
  subtracts the floating `num6`, but `damage.damageResult` is assigned `(int)num6`
  before display. Therefore pre-minimum sub-one HP losses can display **0**.
  Separately, an MP shield calls `UseMp((int)num6)` and sets the absorbed damage
  to zero, which really could absorb sub-one damage without losing MP before
  the minimum-one patch. Current minimum-one does not ceil all values: e.g.
  1.8 remains 1.8 before native absorption.

The installed assembly hash still matches the native evidence in
[domain findings](domain-findings.md). These native paths were re-read from the
matching decompilation; the source was not added to this repository.

### Confirmed fairness limitations

The frequency estimator shares one counter across an attacker's weapon, magic,
accepted DoT pulses, artifact procs and companions against the same player.
Positive attempts count **before** budget admission, including budget-starved
attempts and later shield absorption; early vetoes and rejected debuff pulses
do not. Incidental damage can therefore penalize a later slow attack and spend
its shared budget. There is no weapon-cooldown or attack-activation model.

For a fixed per-hit amount, square-root scaling still lets total throughput grow
roughly with the square root of attack frequency, until the budget binds. It
does not equalize different weapons' DPS.

Per-hit softening leaves damage up to 5% max HP unchanged, then approaches a
20% max-HP ceiling. The burst bucket also stores at most 20% max HP, no matter
how many seconds the player waits. Splitting damage across small hits can avoid
more of this nonlinear compression, while a long-cooldown attack cannot store
several seconds of unused refill.

The one-point minimum introduces a second imbalance: once every frequency-scaled
result is below one, further frequency reduction no longer changes admitted
damage. Faster weapons buy more one-point hits until budget credit runs out.

An isolated C# probe linked the current production `FriendlyFireHitFrequency`,
`FriendlyFireBalance` and `FriendlyFireDamageBudget` helpers. It simulated 60
seconds and measured seconds 30–60, using max HP 100, scale 0.05%, refill 70%,
frequency on, no role reduction/defenses/other effects, and no death reset.

| Native resolved damage and cadence | Native DPS | Modeled DPS without positive minimum | Current DPS with minimum |
| --- | ---: | ---: | ---: |
| 200 per hit, 20 hits/sec | 4,000 | 0.913 | 20 |
| 8,000 per hit, once per 2 seconds | 4,000 | 2 | 2 |
| 2,000 per hit, 20 hits/sec | 40,000 | 9.134 | 20 |
| 120,000 per hit, once per 3 seconds | 40,000 | 5.595 | 5.595 |

The no-minimum column disables only the final/minimum-reservation rule in this
arithmetic experiment; it is not a replay of a historical game executable.
Actual weapon multipliers, defenses, procs and native hit admission differ.
These examples show a confirmed model bias, not proof of the cause of a
particular live disconnect or lost hit.

Another probe submitted 50 one-point candidates at the same timestamp:
20 received damage allowance, 30 received zero, total damage 20. There is no
deferred repayment. The current 6,188 executable friendly-fire hook checks pass;
passing the mechanics tests does not establish weapon balance.

### Recommended follow-up design

1. Add bounded, opt-in diagnostic counters for native veto, effect cooldown,
   pre/post-frequency damage, budget clipping, actual HP/shield/MP loss, and
   nested-proc rejection. Report aggregate samples rather than logging every hit.
2. Prefer fractional internal damage and accurate low-damage feedback. If native
   integer MP consumption requires a remainder, carry that fraction within the
   same pair/lifetime accounting instead of granting free absorption. Making the
   per-hit minimum optional would revise the previous always-at-least-one
   requirement: exact proportional damage at arbitrary rates and one damage on
   every positive hit cannot both be guaranteed.
3. Separate direct attack cadence from incidental DoT/companion/proc hit counts,
   while retaining one shared damage budget so splitting sources cannot multiply
   the total allowance. Avoid treating every small proc as another full attack.
4. Reconsider per-hit softening together with burst capacity. Prefer rate/attack-
   burst normalization with a separately configurable single-hit safety ceiling,
   or a higher validated allowance for genuinely slow attack activations. A
   larger fixed bucket alone does not fix the existing 20% per-hit asymptote.
   Define intentional one-shot protection versus slow-weapon DPS explicitly.
5. Keep budget clipping explicit. Do not silently replay excess damage later:
   delayed damage can outlive a target, role or match. If every admitted hit must
   stay visibly positive, use a separately designed continuous attenuation policy
   and accept its limits rather than claiming a strict cap never suppresses hits.

Recommendations are not implemented. In particular, raising refill alone cannot
fix the low-scale minimum bias when both builds already stay below the budget.
