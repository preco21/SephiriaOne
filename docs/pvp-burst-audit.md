# Slow burst versus split damage audit

`0.97.1` (2026-10-27) changes the stored-burst default from **75% to 70%**
of victim maximum HP. New/omitted settings and burst resets use 70%; explicit
saved values remain unchanged. At the default 70%/s continuous refill, an empty
reserve recovers in one second. The 0.88.0 comparisons below intentionally retain
**explicit 75%** for their historical measurements; algorithms are unchanged.

`0.90.0` adds optional [lower-bound assistance](pvp-lower-bound.md) before this
shared reserve. It supplies a small separate allowance for weak hits, applies
rapid-hit scaling to original and bonus damage together, and still funds their
combined amount through the upper reserve below. Default off; the unassisted
comparisons in this document remain valid.

## Implemented shared burst (`0.88.0`, 2026-10-22)

The low per-hit curve is removed for **all player-victim PvP damage**, including
weapons, magic, artifacts, owned companions, reflection and admitted debuff ticks.
Retaining it on selected categories would leave packet-size bias between weapons
and effects. All sources spend the same attacker/opponent pool, with no extra
allowance per weapon, spell, projectile or artifact.

Stored capacity now defaults to **75% of victim maximum HP**. Continuous refill
stays **70% max HP/second** by default. Optional hit-frequency scaling, its
1s / 4 hits/s / 0.5 defaults, direct/incidental history separation and independent
effect clocks remain in place. Native stats and PvE damage are unchanged.

After native resolution and the friendly-fire percentage, the current order is:

1. Existing effect-specific admission checks.
2. Optional direct/incidental frequency reduction.
3. Shared recoverable reserve, preserving the positive-hit floor.
4. Incoming role modifier, final positive minimum, native shields/HP/armor and
   refund of genuinely unused funded credit.

The former `FriendlyFireBalance.Soften` helper is deleted. No new histories,
per-frame work, allocations or network messages are needed. Existing connection,
life, floor, session and revision ownership remains the sole reset mechanism.
Live capacity/refill edits preserve credit: larger capacity must refill after
it is observed, and grants no immediate or retroactive free burst.

### Verified comparison

Fixture: 150 max HP, 0.3% damage scale, frequency **off**, full reserve, no shield,
role modifier or amplification limit. Inputs below are already native-resolved
and PvP-scaled, not raw weapon coefficients.

| Input damage | Old curve / 20% reserve | New / 75% reserve | New / explicit 20% reserve |
| --- | --- | --- | --- |
| 100 | 25.598 | 100 | 30 |
| 130 | 26.509 | 112.5 | 30 |

A 2.5-second pause restores the new reserve at either 70%/s or 300%/s.
Splitting 100 into 16 simultaneous hits still deals 100. Splitting 130 into 16
simultaneous hits deals 114.5: 112.5 funded damage plus two minimum-one hits after
exhaustion. The same total spread over a second can deal 130 because refill is
allowed during that time. There is no additional per-packet compression advantage.

Mandatory minimum-one damage means arbitrarily many hits cannot obey a strict
DPS ceiling. Existing cadence controls remain available for runaway sources.
Role modifiers and independent attackers also change final damage.

**300%/s remains a high sustained damage setting.** At 75% reserve, it refills in
0.25s; 70%/s refills in about 1.071s. This fix preserves larger isolated hits; it
does not impose a slow TTK or override the host's saved refill choice.

### Settings and upgrade behavior

Use **Rapid Hits / 연속 타격**. Stored burst determines how much a fresh heavy hit
or barrage can use; refill determines recovery over time. Commands:

```text
/one friendlyfire rapid burst 70
/one friendlyfire rapid burst reset
/one friendlyfire refill 70
/one save
```

The single-field reset changes only burst capacity, retaining cadence, refill,
K/D/A and the match. Explicit saved values are preserved, including 20% / 300%.
Change burst to 70 (or reset that field) if it still shows 20. Presets that omitted
the old default inherit 70. No new preset field/schema is required. Bundled EN/KO
controls, help and status explain the new shared reserve. Custom translations
can add the changed keys; missing keys retain normal English fallback.

### Validation and limits

The new regression failed on heavy-hit suppression before the change and passed
after removing the curve. Release build: zero warnings/errors. Combat fixtures,
runtime/session, localization, policy/preset and installed-game contract tests
pass. Warm cadence/guard/Harvest probes allocate zero bytes. `--burst-audit` now
compares current results with historical math kept only inside the diagnostic
(26 checks). Legacy 20% lifecycle fixtures remain explicit; new tests cover 75%,
equal-total packets, shared sources, shields, recovery, partially spent reserve
across max-HP changes and saved-value/reset behavior.

These are fixtures and installed-game static contracts, not a live Black Halberd
or Arrow Rain multiplayer test. Nothing was deployed.

## Historical investigation before the fix

2026-10-22, current runtime 0.87.0. Investigation only: no production PvP tuning
was changed. The user's follow-up confirms reproduction with rapid-hit balancing
off, continuous refill 300%/s, stored allowance 20%.

## Confirmed main cause

`FriendlyFireRuntime.Scale` always calls `FriendlyFireBalance.Soften` when PvP
balancing is enabled. The optional rapid-hit toggle is consulted later. The
softener has no elapsed-time or weapon-cadence input: a heavy hit after a long
pause receives the same compression as a rapid hit of that size.

Let H be the victim's actual maximum HP and D be positive damage **after native
pre-shield calculations and the ordinary friendly-fire damage percentage**.

- D <= 0.05 H: unchanged.
- Otherwise: S = 0.05 H + 0.15 H / (1 + 0.15 H / (D - 0.05 H)).
- S approaches 0.20 H, regardless of how large D becomes or how long the attacker
  waited. This ceiling is separate from the stored burst setting.

At H=150, assuming no additional shields/role multipliers:

| Balancing off, scaled damage | Per-hit softened damage | Full 20% budget | HP loss | Native displayed integer |
| --- | --- | --- | --- | --- |
| 100 | 25.597826 | 30 available | 25.597826 | 25 |
| 130 | 26.508621 | 30 available | 26.508621 | 26 |

The full allowance is not even the limiting stage in these two examples.
The native HP value is float; native `damage.damageResult = (int)num6` truncates
the number displayed, explaining a visible 25 for a 25.60 HP loss.

## Full path to final HP

1. The weapon/spell creates `DamageInstance.damage`, with native attack modifiers
   and any effect-specific callbacks. Eligibility, evasion, invulnerability,
   guard/parry and native veto paths can reject the attack before HP calculation.
2. `UnitAvatar.ApplyDamage` constructs its resolved damage: final multiplier,
   additive attacker/owner All Damage Amplified, companion/elite/weapon bonuses,
   debuff bonuses where applicable, additive rates, critical/execution outcomes,
   defense-to-attack, incoming reductions, elemental defenses, Defense/penetration,
   true damage and Toughness. The native positive minimum runs here.
3. If enabled, the separate PvP amplification control substitutes the combined
   All Damage Amplified bonus at its native consumption in step 2. The screenshot
   has this toggle on, with 50/100 limits. It can independently lower the input to
   later stages. It does not explain the isolated reproduction: the diagnostic
   leaves it off. Do not infer a simple whole-hit ratio because native bonus
   sources are not all combined multiplicatively.
4. Our hook runs **once**, immediately before native Shield absorption. It takes
   resolved damage R and multiplies by friendly-fire `DamageScale`. For the
   screenshot's 0.3%, this is R × 0.003, not R × 0.3. With no other defenses,
   R=33333.333 or 43333.333 produces the example D=100 or 130.
5. PvP balancing applies the per-hit S curve above. Independent artifact/debuff
   admission clocks can reject prohibited repeated effects; these are not the
   ordinary direct weapon hit's per-hit magnitude reduction.
6. Optional direct/incidental hit-frequency scaling multiplies S by
   `min(1, (max(1, rate × window) / recentCount)^exponent)`. With the toggle off,
   this stage is skipped. With the screenshot's 1s/6/0.5 values, the first six
   retained hits are unchanged by this stage.
7. The shared attacker/victim pool clips damage to stored credit, with the
   positive-hit minimum preserved. Capacity 20% means at most 0.20 H stored.
   Refill 300% means 3 H **per second**, not 3 H stored. Empty-to-full takes
   0.20 / 3 = 0.066667s. Credit never accumulates beyond capacity after 2–3s idle.
8. Play Tag/Zombie incoming modifiers apply where relevant. Refill modifiers
   affect step 7's recovery. The final positive minimum is applied once, before
   shields; it does not turn later shield absorption into HP damage.
9. Native Shield and MP shield consume damage. Native super-armor can then reduce
   HP damage to 65%, or 50% for its hard-mode setting. Overkill is clamped to
   remaining HP, `damageResult` is cast to integer, and `Networkhp` loses the
   remaining float amount. Native callbacks/replication and refund of genuinely
   unused budget follow. Later healing may also change observed net HP loss.

Therefore the player's two displayed numbers alone do not establish their exact
native raw damage, max HP, defenses or active amplification reduction. The test
isolates a sufficient cause and matches the reported scale of loss; it is not a
claim to have replayed their exact Black Halberd build.

## Split-damage bias

With H=150, 20% capacity and 300%/s refill, the same total D=130 produces:

| Controlled attack pattern | Current damage |
| --- | --- |
| One hit; rapid-hit toggle off | 26.509 |
| 16 equal hits over 1s; rapid-hit toggle off | 129.730 |
| 16 equal hits over 1s; rapid-hit toggle on, rate 6, exponent 0.5 | 109.052 |

Small packets lie near/below the 5%-HP knee, so each escapes most compression.
The pool replenishes fast enough to fund them. This is a mathematical partition
bias, independent of a special Arrow Rain exemption. Actual Arrow Rain creates
multiple `EDamageFromType.Magic` bullets in `ActiveSkill_ArrowRain.OnServerUpdate`,
each with its own damage and possible Frostbite application. Actual upgraded
counts, damage and impact times were not recovered from the user's session.

Turning rapid normalization off cannot fix the single-hit curve. Raising stored
capacity alone cannot fix it either. Conversely, removing the curve alone while
retaining 20% capacity still limits a fresh 130 hit to 30 at H=150. Both layers
must be considered. Refill 300% deliberately permits very high sustained output;
it cannot also be expected to enforce a much slower DPS target.

## Models worth comparing

1. **Raise or expose the per-hit knee/ceiling.** Smallest change, but still rewards
   splitting the same raw damage into more packets. Useful as an explicit optional
   anti-one-shot ceiling, not as the main fairness mechanism.
2. **Shared recoverable burst allowance (recommended first change).** Remove the
   low unconditional 5%→20% curve from ordinary direct hits; let the shared
   attacker/opponent damage pool govern them. Set stored capacity high enough to
   permit a meaningful isolated heavy hit, independently of refill/DPS. A trial
   range of 60–75% max HP capacity is reasonable for evaluation, not a committed
   default. Recovery must then be chosen deliberately: at 300%/s even that pool
   fills in 0.20–0.25s. All direct/magic/proc damage continues sharing the same pool;
   no extra allowance per projectile, artifact or damage ID. Slow attacks regain
   their reserve between uses; barrages exhaust it. Existing constant-size state
   and arithmetic suffice, with no per-frame work or weapon whitelist.
3. **Cumulative-damage softening.** Apply diminishing returns to accumulated damage
   pressure, charging the marginal difference between the pre/post-hit curve,
   rather than softening every packet independently. This can make a simultaneous
   split burst equivalent to its combined hit while progressively compressing
   sustained output. It needs a bounded recovery model and careful nested-hit,
   shield-refund and live-setting semantics. A count threshold alone cannot provide
   this equivalence. Prototype this before replacing the proven shared journal.

The existing mandatory minimum-one damage creates an unavoidable exception:
arbitrarily many hits each dealing at least one cannot obey a strict total-DPS
ceiling or perfect packet-size equivalence. Preserve responsiveness as requested,
document the lower-bound tradeoff, and retain targeted projectile/effect-cadence
limits for runaway sources. No proposal should silently discard exhausted-budget
hits or charge slow attacks again for unrelated incidental hit counts.

## Reproducible evidence

Run the explicit diagnostic (not part of deployed gameplay):

```powershell
dotnet run --project tests/SephiriaOne.FriendlyFireTests -c Release -p:DeployMod=false -- --burst-audit
```

It invokes the actual addon Harmony hooks on the native-shaped combat fixture,
checks rapid-off/300%/20%, repeats after 2.5s and at 100% stored capacity, and
compares packet patterns using production softener/budget/frequency arithmetic.
Twelve diagnostic checks pass. The hook fixture is not Unity or live networking.

Installed native boundary inspection uses Assembly-CSharp SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`, MVID
`ccf97024-28bd-48f1-9666-c768c2bd4b2b`, the matching local decompilation and the
existing installed-assembly transpiler tests. Main symbols: `UnitAvatar.ApplyDamage`,
`FriendlyFireTranspiler.Rewrite`, `FriendlyFireRuntime.Scale`,
`FriendlyFireBalance.Soften`, `FriendlyFireRuntime.LimitBalancedDamage`,
`FriendlyFireDamageBudget.Take`, `FriendlyFireRuntime.LimitAmplification`.

The user-provided [Black Halberd reference](https://en.namu.wiki/w/%EC%84%B8%ED%94%BC%EB%A6%AC%EC%95%84/%EB%AC%B4%EA%B8%B0%20%EA%B0%95%ED%99%94#%ED%9D%91%EC%B2%A0%20%ED%95%A0%EB%B2%84%EB%93%9C)
was unavailable to the web reader. No weapon coefficients are inferred from it.
The two screenshots document settings; they do not establish impact timestamps,
target max HP, native proc totals or current runtime values at the instant of a hit.
