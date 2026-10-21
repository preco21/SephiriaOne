# Additional PvP defensive effects audit

2026-10-21, addon `0.81.0`. These limits use the existing optional
**PvP balancing** toggle (`/one friendlyfire balance on|off`), default off.
No new preset fields or client addon requirements.

## Changes

| Effect | Native risk | Balanced PvP behavior |
| --- | --- | --- |
| Elastic Band / 탄성 밴드, item 1285 | Every evade restores 1/1/2 dash charges with no cooldown. Battle Bracelet of Lerid / 레리드의 전투 팔찌 (1279) makes dash-invulnerable hits trigger the same evade event, potentially funding repeated invulnerable dashes. | At most one native dash-refund callback per defender every **2 seconds**, across all attackers and owned companions. The dodge itself, refund amount and other evade subscribers remain native. |
| Master's Tsuba / 명인의 코등이 (1316) and Library Minute Hand Miniature / 도서관 분침 미니어처 (1058) | Katana's 0.125-second counter can become `(0.125 + 0.3) × 2.2 = 0.935 seconds`. Dagger uses the same extended-counter producer. | Identified extended weapon counters protect against hostile players for at most **0.35 seconds per activation**, plus native latency allowance. Shorter native windows are not extended. |

The refund gate applies only to the exact current hostile PvP hit, against an
eligible player, while balancing is on. Normal friendly fire, Deathmatch,
Tournament, Play Tag and Zombie Survival use the same qualification. Same-team,
unselected and protected/countdown pairs continue following the shared rules.
Incoming enemy attacks and enemy/companion victims retain native behavior.

Counter duration is adapted at the damage consumer. The addon never changes
native stats, counter counts, timers or active flags, so simultaneous PvE retains
the full native protection and disabling balancing takes effect immediately.
Missing/unknown controller provenance or additional counter contributors are
left native. Paid parry remains a separate branch covered by the
[shield MP floor](pvp-shield-mp.md).

**Counter limitation:** native `StartFuryInvincible` resets its timer even when
already active. Subsequent valid weapon actions can refresh the 0.35-second
window. This is not a maximum total continuous immunity period or an action-rate
limit. Native negative elapsed time for latency compensation is preserved.

## Other audited effects retained

| Item or mechanic | Finding and reason to preserve |
| --- | --- |
| Shimmering Eye / 일렁이는 눈 (1250), `Charm_ShadowEye` | Guaranteed dodge/retaliation requires 16 qualifying critical/execution callbacks, collected no faster than once per 0.15 seconds, and consumes readiness. A charged defensive effect, not continuous immunity. |
| Broken Sapphire / 부서진 사파이어 (1077) | One guaranteed dodge, consumed through the native absolute-evasion flag. Serialized recharge is **16/14/12 seconds**, despite different source field initializers. |
| Smoke Screen / 연막 (3030) | Actual skill lifetime 10 seconds, radius 1.5, three evades per player per instance. Nominal 999-second buff duration is not the effective duration; the skill removes it. |
| Cintamani Stone / 친타마니 돌 (1146), additional lives | Consumes an item or life count. Preserve native lethal saves and revival protection, which are also needed by match respawns. |
| Bloodletting Gearblade / 방혈 장치검, `WeaponAddonGreatsword_BoneBlood` | Actual reduction 25%, transformed maximum HP 50; engagement/removal tracks one contribution. No unbounded stacking found. |
| Pinwheel / 바람개비 (1092) | Stale internal asset name suggests dodge-duration extension; actual serialized stats are attack speed and Evasion. Existing stat-based Evasion cap already covers it. |
| Water Elemental's Paw Wax / 물정령의 발왁스 (1127) | Duration-extendable dash-attack invincibility could overlap its cooldown, but installed item `activeType` is **Disabled**. No new patch for disabled content. |
| Freeze and generic protection buffs | Actual `CharacterDebuff_Freeze` stuns; the separate invulnerable `StatusEffectModule_Freeze` had no instantiating reference in the audited source. Other invulnerability assets were generic/boss/fanatic content, with no ordinary obtainable artifact producer established. |
| Debuff immunity and element resistance | No additional artifact/status producer of `DEBUFFIMMUNITY` or full received-damage reduction found in the bounded asset/source scan. Four serialized elemental/physical defense producers were boss difficulty modifiers, not player artifacts. A native stat consumer alone does not establish an artifact exploit. |

Previously handled White Bread, stat-derived Evasion, guard/parry costs, MP
shields and automatic shield absorption were excluded from new balance changes.
In particular, do not suppress `OnEvade` globally: doing so would stop limited
guaranteed-dodge barriers from consuming their charges.

## Implementation and extension guidance

- `FriendlyFireDefensiveEffects.cs`: narrowly scoped refund prefix, counter
  scalar adapter, cached readonly field access, installation contract checks.
- `FriendlyFireBalanceBudget.cs`: the existing receipt-owned `BalanceActor`
  stores one refund window. Combat/participant/settings epochs, life and floor
  changes retire it. Disconnect/reconnect gets a new receipt. Clock rollback
  also retires stale elapsed state. Artifact replacement does not bypass it.
- Reserve refunds before invoking native code, preventing nested callbacks from
  generating extra restores. PvE refunds neither spend nor reset the PvP window.
- Hook installation uses the existing rollback path. The refund callback must
  retain its exact native producer shape; the counter adapter requires one
  signed-byte read and the audited positive-count comparison before `OnCounter`.
- No frame polling, inventory enumeration, new RPCs or native state mutation.
  State grows with existing combat participants; weak receipt ownership provides
  the same retirement behavior as the damage-budget infrastructure.

Future defenses should be qualified at their actual producer/consumer, not by
globally removing invulnerability. Verify serialized assets as well as code:
initializers, stale item names and long nominal buff durations were misleading
in this audit. Preserve native charge consumption and PvE nested-hit behavior.

## Evidence and verification

Installed `Assembly-CSharp.dll` MVID:
`ccf97024-28bd-48f1-9666-c768c2bd4b2b`; SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
The cached decompilation was identity-checked against this installed binary.
Asset coverage: MonoBehaviour payload searches and targeted decoding in
`resources.assets`, `sharedassets0.assets`, `sharedassets1.assets`, and
`sharedassets2.assets`. `sharedassets0.assets` SHA-256:
`73135788932f461054da76fde2bba1793c64748e77e9dfa3028a1ab6da47ee36`.

Key asset anchors: Elastic Band resources29194 / component187200; Battle
Bracelet resources29193 / component187197; Master's Tsuba item179890 /
component189648; Library Miniature resources28916 / component186417;
Shimmering Eye resources29215 / component210151; Broken Sapphire resources28928 /
component199737; Smoke Screen skill215392 / buff214751; disabled Paw Wax
resources28958 / component215960 / buff218690; Bone weapon component190634.

Executable fixtures cover the real Harmony prefix/transpiler, two-second
boundary, shared defender refunds, re-equipping, companion sources, concurrent
and nested PvE, callbacks, exceptions, life/connection/floor/settings resets,
short/extended/unknown counter windows and readonly timer behavior. Both paths
allocate **0 bytes per 1,000 warmed fixture hits**. Installed-game IL contracts
verify native producer/consumer shape, native refund transport, metadata
preservation and rejection of changed/ambiguous code.

Static audit and executable fixtures are not live multiplayer verification.
Recheck latency, repeated counter actions and dash-refund behavior with a stock
guest before deployment. External addons, dynamically created effects and all
scene-specific sources were not exhaustively traced. Game binaries, decompiled
source and assets remain outside the repository. No deployment was performed.
