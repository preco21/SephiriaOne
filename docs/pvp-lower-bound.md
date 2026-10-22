# PvP lower-bound assistance

Implemented in `0.90.0`, 2026-10-23. **Off by default**. Host-only; uses the
existing native damage path, with no new guest assets, messages or stat writes.

## Controls

Open **PvP Balance → Weak-hit assistance / 약한 타격 보조** in the scrollable
left pane. The independent right-hand debuff controls retain their layout.

```text
/one friendlyfire lower-bound on
/one friendlyfire lower-bound off
/one friendlyfire lower-bound reference 4
/one friendlyfire lower-bound refill 8
/one friendlyfire lower-bound reference reset
/one friendlyfire lower-bound refill reset
/one friendlyfire lower-bound reset
```

Reference defaults to **4% of the opponent's maximum HP**, adjustable from
0.1 to 25%. Bonus refill defaults to **8% of maximum HP/second**, adjustable from
0.1 to 100%. Both accept up to two decimal places. Family reset disables the
feature and restores both defaults; numeric resets retain its toggle. The
feature also requires the main **PvP balancing** toggle. Preset v49 saves all
three values; older presets load it disabled with the new defaults.

Settings edits are immediate, preserve the active match and existing combat
accounting, and do not reset K/D/A, participants, role assignment or frequency
history. This applies to casual friendly fire, Deathmatch, Tournament, Play Tag
and Zombie Survival through the shared damage pipeline.

## Why use an opponent-relative allowance?

A flat `max(damage, 4% HP)` per hit rewards packet splitting: twenty tiny hits
would each receive a new minimum. A gear/stat power score is also unreliable:
native Planets, MP-scaled Ice Weapons, ordinary weapons and magic have different
damage formulas. See [the Planet audit](pvp-planet-damage.md).

Instead, measure the **actual resolved, PvP-percentage-scaled hit relative to the
opponent's maximum HP**. This reflects the attack's current damage pressure
without a guessed roster-wide offensive score or live stat normalization. It
helps weak positive hits from any build; it does not classify a costume as a
defender or award a bonus because the attacker has high defense/HP.

Let `D` be that positive scaled hit, and `R = opponent max HP × reference`:

```text
requested bonus = max(0, R - D / 2)
fully funded result = D + requested bonus = max(D, R + D / 2)
```

The result is continuous and strictly increasing. Stronger original hits remain
stronger. Hits of at least `2R` receive no bonus and are unchanged by this layer.
Zero, invalid and genuinely rejected hits receive no assistance.

For 150 maximum HP with the default reference, `R = 6`:

| Original scaled hit | Fully funded result, before other limits |
| --- | --- |
| 1 | 6.5 |
| 2 | 7 |
| 6 | 9 |
| 12 | 12 |
| 20 | 20 |

These are not promised final HP losses. Native rounding, frequency scaling,
remaining allowances, role defense and shields still affect the outcome. Some
native damage reductions already contribute to `D`; the uplift deliberately
narrows that resulting damage gap. It does not remove downstream protection.

## Preventing rapid-hit amplification

One small bonus pool belongs to each **attacker/opponent pair**, shared by direct
hits, incidental damage, all Planet types, owned companion effects and admitted
debuff ticks. It has capacity `R` and the configured continuous bonus refill.
Switching sources cannot obtain extra pools. Neither Zombie nor chaser refill
multipliers multiply this bonus pool; they still affect the existing main pool.

Processing order:

1. Native hit/hostility and existing artifact/debuff admission checks.
2. Compute the optional bonus from positive `D`.
3. Apply the existing optional rapid-hit factor to **both** `D` and its bonus.
4. Fund only the available bonus from the shared assistance pool.
5. Feed the combined amount through the existing shared upper damage allowance.
   Original damage has priority; immediately return unfunded bonus credit.
6. Apply incoming role modifiers and the existing native shield/HP handling.
   Reconcile genuinely unused funded credit through the normal hit scope.

At 150 HP, simultaneous attacks share **at most 6 added damage**, not 6 per hit.
Across elapsed time `t`, added damage cannot exceed `6 + 12t` before downstream
reductions (for fixed maximum HP and unchanged default settings). High-frequency
attacks can use the refill continuously, but cannot multiply it by their hit
count. Slow hits recover their small reserve between attacks. Main stored burst
and sustained refill continue to constrain original plus assisted damage.

An exhausted assistance pool leaves the original hit intact. Exhausted main
credit retains the established minimum-one behavior; **there is no guaranteed
3–5% per-hit floor through exhaustion**. Minimum-one rounding can still exceed
the main budget at extreme rates, an existing deliberate responsiveness tradeoff.
This feature does not remove native hit rejection or configured debuff/artifact
interval suppression. See [shared burst](pvp-burst-audit.md) and
[rapid-hit settings](pvp-hit-frequency.md).

## Accounting, lifetime and performance

`FriendlyFireLowerBound` contains the pure curve. `BalanceWindow.Assistance`
reuses `FriendlyFireDamageBudget`; the existing pair receipt owns its lifetime.
Reserve before native callbacks so nested attacks cannot reuse the same credit.
Track only the bonus actually funded by the upper allowance. A late native veto
or overkill returns unused funded bonus first, once; shield absorption counts as
delivered damage. Existing generation/current-hit checks prevent stale refunds.

Connection receipt, life, floor, participant selection, K/D/A/match reset and
runtime teardown follow the existing budget lifecycle. Lower-bound-only edits
do not advance balance/frequency/role epochs. Toggle off/on preserves spent
credit; increasing capacity does not grant an immediate fresh reserve. Capacity
and rate changes use the existing conservative refill arithmetic (no retroactive
credit at an increased rate).

There is no participant scan, per-frame maintenance, new RPC or player-stat
mutation. Enabled processing adds constant-time arithmetic and one small budget
struct to an already allocated pair window. A warmed 2,000-hit fixture measured
zero additional managed allocations. This is allocation evidence, not a live
game frame-time benchmark.

## Verification and live checks

Covered by pure/property tests, real patched combat against game API fixtures,
command/session integration, v49/legacy preset validation, EN/KO catalogs and
installed-game panel contracts. Cases include invalid inputs, 0.3/0.5% scale,
different maximum HP, strong unchanged hits, slow/rapid split streams, shared
Planet/proc sources, exhausted pools, nested hits, native cancellation, shields,
roles, rejoin/life/floor/reset and PvE/off behavior. Live edits are exercised in
all four modes and casual friendly fire.

Not yet verified in a live multiplayer match; no deployment was performed. With
unmodified guests, compare enabled/disabled using one weak slow attack, a rapid
weapon and all Planets at the same damage percentage. Test both Zombie/Human
directions and shields. Tune the reference for occasional weak-hit strength and
the bonus refill for sustained assistance; tune the existing main refill/burst
separately. A video or damage trace can guide subsequent tuning.
