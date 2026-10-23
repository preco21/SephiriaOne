# Optional PvP hit-frequency scaling

## Periodic recovery (updated 0.94.4, 2026-10-24)

The user requested recovery even during uninterrupted attacks, with the new
option **on by default, every 1 second**. It is separate from **History window**
(previously described as hit record time): history measures recent cadence;
periodic recovery restores the pair's configured stored burst allowance and
clears direct, incidental and individual Planet cadence histories together.
Neither control enables PvP balancing itself; recovery requires balanced PvP
but works whether optional hit-frequency scaling is on or off.

Use **Rapid Hits → Periodic recovery / 주기적 피해량 회복** and
**Recovery interval (s) / 회복 주기 (초)**, or:

```text
/one friendlyfire rapid recovery on
/one friendlyfire rapid recovery off
/one friendlyfire rapid interval 1
/one friendlyfire rapid interval reset
/one save
```

Interval range: 0.1..60 seconds, up to two decimal places. One clock belongs to
each directed attacker/opponent pair, shared by all weapons, artifacts, owned
companions and hit categories. It starts at the first eligible balanced hit and
keeps fixed deadlines during continuous fire. The first eligible hit at/after a
deadline recovers capacity; missed intervals collapse to one recovery, never
multiple banked bursts. No firing pause, timer job or per-frame scan is needed.

Continuous refill continues between recoveries. This intentionally raises the
available sustained damage: at shipped 75% stored capacity and 1 second, the
additional allowance can approach 75% of opponent maximum HP/second on top of
continuous refill, before roles/defenses and minimum-hit effects. Shorter
intervals grant more frequent bursts. It is not a strict fixed-window DPS cap.

Live recovery toggle/interval edits rearm on the next eligible hit without
immediate credit. An independent revision observes off/on changes even when no
hit occurred between the commands. Repeating an unchanged command preserves the
deadline. Changing history parameters, stored capacity or continuous refill does
not restart the periodic clock. Full shared lifetime resets (life, floor,
participant/session/KDA epochs, balance activation) also discard that clock.
All PvP sub-modes use this same path, including Zombie and Play Tag role factors.

Recovery leaves lower-bound assistance, debuff schedules and autonomous-artifact
cooldowns untouched. Those cadence gates run before recovery admission; rejected
effects cannot reset their own cooldowns. The existing positive-hit floor remains
when the burst runs out. Native defenses still apply. PvE is untouched.

The hit-history arrays are reused across recoveries; a separate recovery
generation prevents a pre-recovery outer hit from refunding into a nested new
reserve, while still returning unused assistance to its unchanged pool. Fractional
missed deadlines cannot recover twice at the same timestamp. Explicit
`recovery reset` remains available during compatibility faults without allowing
an ordinary enable command. Tests cover uninterrupted 10 Hz fire, exact deadlines, shared categories,
independent opponents, long idle, live edits, nested refunds, floors and current
PvP modes. Warmed periodic recovery allocated zero managed bytes across 3,000
hits in the executable fixture. These are not live multiplayer/FPS measurements.

Original 0.92.0 verification: Release build passed with zero warnings/errors; 15,654 executable
combat checks and 14,193 runtime/session checks passed. Portable policy/catalog
checks and installed-game/UI/Harmony contract checks passed. Independent review
findings were reproduced in regression tests and corrected before completion.

`0.94.4` changes only the default/reset interval from 3 seconds to 1 second.
Explicit saved custom intervals stay unchanged. Preset v51 stores nondefault
recovery settings; older presets that omit them
adopt the user-approved on/1s default. Existing history, burst and refill values
are preserved. Recovery off restores continuous-refill-only behavior. No
deployment or live game test was performed.

For 0.94.4, the non-deploying Release build, policy/catalog, localization and
panel checks passed, along with 15,713 executable combat checks and 14,419
runtime/session checks. Recovery tests exercise the new default during continuous
fire; history-only and continuous-refill-only fixtures explicitly disable recovery
so they continue checking those independent controls.

Planet handling (`0.89.3`): [Planet damage audit](pvp-planet-damage.md) separates each audited
Planet type's cadence from other Planets and incidental damage. Copies of the
same type still share history; all sources still share one damage allowance.

Since `0.88.0`, the [shared-burst fix](pvp-burst-audit.md) removes the separate
per-hit curve and raises default stored capacity to 75% max HP, preserving
infrequent heavy attacks while all hit types continue sharing one reserve.

Added in `0.60.0`, 2026-10-16. Default **off**, host-controlled, using the existing
damage path for unmodified guests. Requires friendly fire **and** PvP balancing.
The user's selected starting threshold is **4 hits per second per opponent**.

Updated in `0.77.0`, 2026-10-21: recommendation 3 from the audit below is
implemented. Direct attacks and incidental damage now have separate cadence
histories but share one allowance. The user's additional responsiveness request
also replaces zero-budget rejection with a positive pre-shield minimum.

## Controls

Since `0.83.0` (2026-10-22), use **Rapid Hits / 연속 타격** beside **PvP Balance**.
Enable PvP balancing on the latter page for these controls to affect combat.
The frequency toggle remains default **off**. Stored capacity changed to 75% in
`0.88.0`; periodic recovery was added in `0.92.0`.

| Control | Default | Range | Effect |
| --- | --- | --- | --- |
| Detection window | 1 second | 0.1–10 seconds | How long recent hits remain in the rolling estimate. |
| Full-damage rate | 4 hits/second | 0.1–100 | Full damage up to `max(1, rate × window)` recent hits. |
| Reduction exponent | 0.5 | 0–2 | Above threshold, multiply by `(threshold / count)^exponent`; higher reduces more, zero removes frequency reduction. |
| Stored burst allowance | 75% of victim max HP | 1–100% | Maximum credit stored for this attacker/opponent pair. |
| Continuous refill | 70% of victim max HP/second | 1–300%/second | Adds allowance over elapsed game time, up to capacity. |
| Periodic recovery | On | On/off | Restores shared capacity and clears hit history even during uninterrupted fire. |
| Recovery interval | 1 second | 0.1–60 seconds | Fixed deadlines per attacker/opponent; missed recoveries never accumulate. |

All numeric values accept two decimal places. Hit history ages out and damage credit
refills continuously; since `0.92.0`, the separate periodic-recovery control above
also resets shared capacity/history by default. Base empty-to-full recovery from
continuous refill alone is `burst / refill` seconds (about
1.071 seconds by default). Play Tag/Zombie refill modifiers can change that time.
The UI shows applied values and an example of reduction above the expected rate.

The shared budget still applies when the frequency toggle is off. A heavy hit
can use the entire stored reserve; smaller packets spend that same reserve.
There is no separate low per-hit ceiling. Native defenses, role modifiers and
separate DoT schedules still apply. Explicit saved capacity/refill settings are
preserved; omitted capacity inherits 75%. Use `rapid burst reset` if a saved
20% setting is still limiting heavy hits.

The page uses the same host commands as chat:

```text
/one friendlyfire hit-frequency on
/one friendlyfire hit-frequency off
/one friendlyfire hit-frequency reset
/one friendlyfire rapid window 1
/one friendlyfire rapid rate 4
/one friendlyfire rapid exponent 0.5
/one friendlyfire rapid burst 75
/one friendlyfire refill 70
/one friendlyfire rapid window reset
/one friendlyfire rapid reset
/one friendlyfire status
/one save
```

`hit-frequency reset` switches only the frequency toggle off. `rapid reset`
restores the four original numbers plus recovery on/1s, without changing the
frequency toggle, continuous refill or other settings. Each
numeric key also accepts `reset`; `refill reset` restores refill separately.
The preference can be configured while
balancing is off, and takes effect when its prerequisites are enabled. Toggle
edits preserve the current match, K/D/A, damage-budget credit, refill rate, guard
cost and DoT intervals. Repeating `on` does not reset history. Save stores only
the preference, never runtime hit history. Enabled settings use preset v29;
earlier schemas inherit off, and off adds no new row or version requirement.
Nondefault rapid parameters require preset v44; older schemas inherit these
defaults. Runtime counters and credit are never saved.

## Damage model

The existing native defenses and ordinary friendly-fire damage percentage still
run first. The sequence within PvP balancing is:

1. Existing debuff/autonomous-artifact damage admission.
2. Optional frequency multiplier: `min(1, (max(1, rate × window) / recentHitCount)^exponent)`,
   using the hit's direct, other-incidental or audited Planet-type history.
   Defaults retain `sqrt(4 / count)`.
3. Shared per-opponent damage budget (configured capacity and refill),
   clipped continuously to available credit with a positive minimum at exhaustion.
4. Current role incoming modifier, positive damage minimum (since `0.67.1`),
   native shields/HP and unused-budget refund.

| Recent hits against this opponent (default settings) | Per-hit frequency multiplier |
| --- | --- |
| 1–4 | 100% |
| 8 | about 71% |
| 16 | 50% |
| 32 | about 35% |

The first hits retain their strength. Further rapid hits progressively lose
damage without changing attack speed or discarding native effects/callbacks.
When the budget is already the tighter limit, frequency scaling may not further
reduce that hit. Since `0.77.0`, the [positive-hit minimum](pvp-balancing.md#minimum-positive-damage-0671-2026-10-20)
also survives an empty pool. In pre-role units, `floor = 1 / incomingScale`,
`allowed = max(floor, min(scaledDamage, available))`, and
`charged = min(available, allowed)`. Only funded credit is reserved/refundable.
Unfunded floor damage creates neither debt nor later credit, including when a
nested proc, armor, shield, native veto or overkill changes actual delivery.

This preserves a minimum of one before native shields/armor for valid positive
damage and positive configured scales. It does not revive invalid/zero hits or
bypass native defenses and effect schedules. There is no delayed damage queue.
At extreme rates, many minimum-only hits can exceed the refill rate: this is
intentionally **not a strict DPS cap**. Low-scale, rapid-hit fairness is still
limited by the one-point floor; the other audit recommendations remain deferred.

Count positive resolved hit attempts reaching this stage, including minimum-only
hits. This prevents exhausted-budget barrages from
appearing slow. Misses, invulnerability, native guard/parry, invalid or zero
resolved damage, and rejected Shock/Freeze pulses do not add hits. Shield
absorption and later native reductions retain their existing accounting; they
do not roll back the hit count. Weapons and ordinary spells share direct history.
DoT, owned companions, reflection and identified artifact procs share incidental
history. Changing item IDs does not create another counter or damage allowance.
Reflections use the reverse directed pair. Other attackers/victims are independent.

Classification uses the existing captured hit context: companion source identity,
reflection/debuff/artifact scopes, native `fromType=None`, elemental-effect damage,
or a `Charm_` ID prefix. It reads current metadata each hit because damage objects
are pooled. No asset lookup, stack inspection, per-effect dictionary or native
attack-activation model is introduced. Unknown future procs tagged like ordinary
magic may require adding a verified classification rule.

This does not change native player stats, proc chances, status applications,
crowd control, attack cadence or pooled `DamageInstance` values. Existing Burn
lifetime, DoT intervals and Dark Cloud cooldown remain separate restrictions.
PvE, self hits, non-player victims and disallowed participants bypass this scaler.

## Implementation and lifetimes

`FriendlyFireHitFrequency` stores eight counters at `window / 8` resolution
(125 ms at the default). A rolling
sum avoids scanning prior hits; time advancement touches at most eight buckets.
This approximates the selected window: a hit ages out after 0.875–1 windows depending
on its position within a bucket. It has no polling or per-hit collection growth.
There are two such histories; each eight-counter array is allocated only on its
first eligible hit in a pair's lifetime. A hit advances only its chosen history;
warmed hits allocate nothing. Counts saturate safely, invalid clocks
do not modify history, and clock rollback clears stale history.

The struct lives in `FriendlyFireRuntime.BalanceWindow`, using the same weak
connection receipts, score/participant/balance epochs, attacker/victim life
generations and floor identities as the damage budget. Native revival, K/D/A
reset, participant changes, reconnects, floor transitions and mode cleanup
therefore cannot inherit an old pair's hit-rate penalty. Match start/end and
selection continue through the existing shared runtime for casual combat,
Deathmatch, Tournament, Play Tag and Zombie Survival.

`SessionSettings.CombatFrequencyRevision` advances only when the frequency toggle,
window, full-damage rate or exponent effectively changes, or full combat reset
occurs. A pair lazily discards its old frequency
history on the next enabled hit. This revision is deliberately separate from
`CombatBalanceRevision`: toggling frequency must not refill the damage budget,
clear Shock/Freeze/Dark Cloud clocks, reset K/D/A or interrupt a match. Settings
application is centralized so sibling edits and temporary mode overlays retain
the preference.

Capacity/refill edits preserve frequency history and existing budget credit.
On the next hit, a decreased capacity clamps credit; an increased capacity does
not grant credit retroactively above the previously observed cap. It refills
normally thereafter. Refunds obey both the last observed cap and current policy,
including edits inside a native damage callback. Unfunded minimum hits remain
unrefundable. No per-frame worker or per-hit collection is introduced; the
default retains the square-root fast path, with simple fast paths for exponents
0, 1 and 2 and `Math.Pow` only for other strengths above threshold.

All policy, hit counts, scaling and accounting run on the host. Native damage,
shield and death replication continues unchanged; no guest addon, asset or new
network message is required by this implementation.

## Verification and limits

`0.83.0`: non-deploying Release build passed without warnings/errors; 8,914
executable combat checks, 13,868 command/session checks plus 69 Bat checks,
373 rapid-policy checks, 4,423 bundled-catalog checks, 50 localization checks
and panel/native contracts passed. Configured warmed rapid hits allocate zero
bytes. Tests cover new ranges, numeric resets, v1–43 compatibility, atomic v44
loading, saved settings, live match edits, capacity changes/refunds and positive
minimums. Independent review found no actionable issue. The expanded session
test establishes the native participant scope before comparing epoch changes;
initial lazy scope creation is not a command-driven K/D/A reset.

Command/preset tests cover default-off behavior, strict parsing, older schemas,
atomic malformed-preset rejection, sibling-setting preservation and mode overlays.
Executable Harmony fixtures cover the 4/8/16-hit rates, burst/idle/clock behavior,
budget composition, no token refill on frequency toggles, shields, guard,
reflection, nested procs, companions, DoT, authority/participant exclusions,
respawn/reconnection/floor/score cleanup, and zero warmed per-hit allocation.
Session tests cover live edits in every combat mode, saved settings and restarts.
Compiled integration checks verify UI/command wiring and frequency-before-budget
ordering. English/Korean catalogs pass key/placeholder checks.

The `0.77.0` regression suite additionally covers mixed direct/incidental barrages,
pooled ID reuse, independent cadence with a shared pool, exhausted/partially
funded refunds, nested procs, role reductions and floor-only pulse scheduling.
Warmed mixed traffic allocates zero bytes across 2,000 executable-hook hits.
The dated implementation plan records final verification.

These are automated fixtures and installed-assembly checks. Live Unity UI layout
and host/stock-guest gameplay still need testing. Nothing was deployed or launched.

## Low-scale fairness audit (2026-10-20, 0.73.0)

This section records **historical 0.73.0 behavior**, before the `0.77.0` changes
above; zero-budget rejection and a single cadence history no longer apply.
The per-hit curve and 20% default below were also replaced in `0.88.0`; see
the current damage model and shared-burst findings above.
The user
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

Recommendation 3 and the subsequently requested positive exhausted-budget floor
are implemented in `0.77.0`. Other recommendations remain deferred. Raising refill
alone cannot fix low-scale minimum bias when both builds already stay below budget.
