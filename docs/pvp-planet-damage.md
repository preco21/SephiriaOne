# Planet PvP damage audit and cadence fix

2026-10-23, addon **0.89.3**. Report: all seven Planet artifacts, Zombie attacker,
rapid-hit normalization and dynamic Zombie balancing enabled, damage scale
0.3%–0.5%; Planet hits around 1–3 versus Ice Weapons/Fire Arrow around 20–25.

## Confirmed problem and correction

Previously every Planet projectile shared the **incidental** rapid-hit history
with every other Planet, artifact proc, companion and accepted debuff tick from
that owner against that opponent. Fire Arrow instead used the **direct** history.
Independent orbiters therefore inflated one another's rate even if each orbiter
was firing slowly. Their own secondary debuffs could compound that reduction.
This is a confirmed extra reduction, not proof of the exact live damage numbers:
the build's native stats, item levels, target defenses and hit timeline were not
captured. Lower native Planet inputs and depleted shared credit can also matter.

Each of the seven audited Planet types now has its own optional rapid-hit history.
Multiple copies, enhanced shots and ignite-tail shots of the **same type** still
share that history. Fast Planet barrages still get the configured reduction.
Other incidental sources keep their existing combined history; direct attacks
keep theirs. Unknown Planet-like IDs fall back to ordinary incidental handling.
Status effects caused by Planets still use their debuff rules and incidental
history, not the originating Planet's primary-projectile history.

**There is still exactly one damage budget per attacker/opponent**, shared by all
Planets, attacks, companions and procs. No new funded burst is created by switching
Planet, weapon or role. Native base damage, configured damage scale, dynamic
Zombie modifiers, defenses, proc behavior and attack timing are unchanged. The
general autonomous-artifact hard interval continues to exclude Planets.

Classification requires an audited ID, native `None` source / `Projectile` type,
the credited player as the actual origin, and no reflection, debuff, burn-explosion
or synchronous artifact-proc scope. It does not retain pooled packet identity.
Storage is a lazy seven-element array of the existing eight-bucket rate counter.
All counters use the current receipt, life, floor, selection, combat and settings
reset boundaries. There is no per-frame scan, per-projectile tracking or unbounded
source dictionary; a warmed 2,000-hit mixed-Planet fixture allocated **0 bytes**.

## Native evidence and comparison

Inspected installed `Assembly-CSharp.dll` with REA managed inventory, ILSpy C# and
the serialized prefabs in `sharedassets0.assets` using UnityPy. Assembly SHA256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`. Re-check on game updates.
Do not commit the native assembly, prefab export or decompiled sources.

`GreenBat` is a Planet emitter, not a companion avatar. Its four
`CreateBullet` spawn sites use the owning player's avatar and the same source ID
for normal/enhanced shots with/without ignite tails. `Bullet` creates the native
`DamageInstance` and calls the shared `CombatBehaviour.ApplyDamage` path.

| Planet | Exact damage-ID suffix (`Charm_Planet_`) | Item-level base range | Prefab interval / shots per cycle |
| --- | --- | --- | --- |
| Blue | `Blue` | 10–30 | 1.5s / 3 |
| Yellow | `Yellow` | 20–60 | 3s / 1 |
| Sky Blue | `Sky` | 30–60 | 2.6s / 1 |
| White | `White` | 30–72 | 1.2s / 1 |
| Ashen | `Gray` | 15–40 | 2.5s / 1 |
| Dark | `Black` | 50–100 | 1s / 1 |
| Red | `Red` | 26–60 | 2s / 1 |

These are serialized base values, **not final damage or effective attack rates**.
Native `PLANETATTACKSPEED`, enhancement, additional emitters and hit behavior still
modify cadence. Planet emitter asset path IDs are 192899–192905; charm definitions
211478–211484. Exact IDs are an audited allowlist, not a prefix-based exemption.

`Charm_SummonGreenBat.GetDamage` begins with `damageByLevel`. If
`PLANETIGNITEDAMAGE` is enabled, it adds native Burn/Plasma calculated tick damage
times the rarity-dependent count. `GetBatDamage` also uses the native charm bonus;
`GreenBat.CreateBullet` adds `PLANETDAMAGE` and the enhanced-shot 50% bonus.
This addon does not rescale those getters or reuse its PvP DoT limiter there.

By comparison, `Charm_IceSword.GetDamage` (the Plitvice Ice Weapon) starts with
`IceDamage × 0.06 × max(0, MaxMp - PLAYERDEFAULTMP) × 0.5` in the installed prefab,
with Fire substitution and `FROSTRELICDAMAGE` when native conditions apply. Its ID
is `Charm_PlitviceWaterDrop`; it uses the other-incidental rate history. High MP
can make its native input much larger than a Planet's.

`ActiveSkill_Arrow.OnStartMagic`, ID `Skill_FireArrow`, uses the prefab level bases
18/20/22 plus FireDamage ratios 120/240/360%, then its native power and magic/MP
skill bonuses. Its source type is `Magic`, using the direct history. Equal PvP
settings therefore do not imply equal native input damage across these effects.

## Damage trace

1. Emitter creates raw damage using the native formulas above.
2. `UnitAvatar.ApplyDamage` resolves native amplification (including the optional
   PvP All Damage Amplified cap), other bonuses, criticals, target elemental and
   ordinary defense, fixed damage and toughness. Native evasion/block/protection
   can reject the hit before this stage completes.
3. The central hook multiplies **resolved** damage once by the friendly-fire
   percentage: 0.3% means `0.003`; 0.5% means `0.005`.
4. Accepted damage receives optional rapid-hit scaling using its own history:
   `min(1, (max(1, rate × window) / recent hits)^exponent)`.
5. All sources consume the same stored allowance. Capacity is configured burst %
   of victim maximum HP; refill is configured %/s times the **attacker's** current
   role multiplier. A previously saved 20% reserve remains 20%; current default 70% (since `0.97.1`).
6. The **victim's** current incoming-role multiplier applies, then the existing
   positive minimum of 1 before native shields. Fractional damage above 1 remains
   fractional; floating damage labels can differ from actual HP loss.
7. Native shield/MP-shield absorption and later super-armor/HP handling finish the
   hit. Only genuinely unused funded budget is refunded. The positive minimum
   does not undo shields or turn rejected hits into damage.

Dynamic Zombie balancing does not directly multiply a Zombie's outgoing raw
damage. It modifies refill and victim-role damage taken. At 5 Zombies : 1 Human,
after smoothing, a Human receives about **70.37%** incoming damage and Zombie
refill is about **0.704×** global refill. Those apply equally to Planet, Ice Weapon
and Fire Arrow hits against that Human. At parity they are neutral. The static
30% Zombie incoming setting is not an outgoing damage multiplier and is not used
as the dynamic Human defense. Role transitions retain spent credit.

Controlled arithmetic example (not a reconstruction of the reported build):
resolved native damage 5,000, scale 0.3%, default exponent/rate, 5:1 composition,
enough credit, no shields. A first Planet hit after 80 **other** incidental hits
used to deal `5000 × .003 × sqrt(4/81) × .703704 ≈ 2.35`. It now deals
`5000 × .003 × 1 × .703704 ≈ 10.56`. At 0.5% those values are about 3.91 and 17.59.
If those 80 earlier hits are from the **same Planet type**, its frequency reduction
still applies. If the shared allowance is exhausted, the positive floor still
applies regardless of source.

Planet base damage 10–100 without substantial bonuses would become 0.03–0.5 at
the reported scales even **before** frequency/role reduction, hence the existing
minimum of 1. The fix deliberately does not force all artifacts to deal 20–25 or
boost flat native damage to match MP-scaled armaments.

## Validation and live follow-up

- Regression failed before the fix on the fifth different Planet in a single
  low-cadence volley; all seven now retain their independent cadence.
- Executable hooks cover repeated same-type attacks, generic incidental traffic,
  unknown IDs/status-type fallback, pooled ID changes, one shared exhausted
  allowance/refill, frequency off, score/life/floor/reconnect/selection/config reset,
  and matched-input Planet/Ice Weapon/Fire Arrow damage at 0.3% and 0.5% with
  dynamic Zombie balancing on. Existing general incidental policy tests remain.
- Installed-game contracts verify the four native Planet emission sites, source
  IDs, native ignite/Burn/Plasma formula dependencies and Ice Weapon/Fire Arrow
  comparison entry points, alongside existing damage/targeting/native-sync checks.
- No deployment or live multiplayer test was performed. Test on a stock guest
  with the same loadout and settings: compare isolated Planet hits versus mixed
  Planets/procs, frequency on/off, dynamic roles at parity and Zombie majority,
  and both full/exhausted shared allowance. Native damage differences remain.

See [hit-frequency controls](pvp-hit-frequency.md), [shared burst allowance](pvp-burst-audit.md),
and [autonomous-artifact exclusions](pvp-autonomous-artifacts.md).
