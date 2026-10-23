# Autonomous artifact hit-rate limit

Added in **0.86.0**, 2026-10-22. Host-only; uses existing native damage and
replication paths for unmodified guests. No custom client assets or messages.

**`0.89.1` correction:** Dark Cloud is excluded from this limiter. The earlier
implementation treated it as another autonomous attack and used the longer of
the two intervals. That made the general setting override its dedicated control.
Dark Cloud now uses only `/one friendlyfire dot dark-cloud N` (default 2s since `0.94.2`).
Changing the artifact interval/toggle does not reset or modify its strike clock.
Saved settings retain both existing values; no migration or new runtime state.
Release build, combat/runtime regressions, localization and installed-game
compatibility checks passed. The decoupling adds no polling or allocations;
existing Dark Cloud allocation checks still pass. Not deployed or live-tested.

## Controls

**PvP Balance → Artifacts / Debuffs → Autonomous artifact hit limit** has a
separate toggle and minimum hit interval. Default **off / 2 seconds**. Accepts
**0.05–60 seconds**, with up to two decimal places. Requires PvP balancing.

```text
/one friendlyfire artifact-rate on
/one friendlyfire artifact-rate interval 2
/one friendlyfire artifact-rate interval reset
/one friendlyfire artifact-rate off
/one friendlyfire artifact-rate reset
/one save
```

`off` retains the configured interval. `interval reset` restores only 2 seconds.
The family `reset` and UI Reset restore off / 2 seconds without changing the
match, scores, selection, DoT controls, damage credit or other settings. Full
friendly-fire reset also restores these defaults. Preset v47 stores nondefault
`friendlyfire artifact-rate 1` and `friendlyfire artifact-interval N` rows;
missing rows and older presets inherit current defaults. EN/KO text is bundled.

## Audited coverage

The limit is per **attacker connection, artifact identity and opponent
connection**, not per projectile, artifact copy or elemental variant. An
admitted impact starts the interval. Additional impacts inside it are rejected;
they are not queued, merged or compensated later. Native cooldown reduction
can emit more attacks but cannot shorten the PvP damage interval.

| Installed artifact | Native identity | Automatic path |
| --- | --- | --- |
| Guillotine of Bitter Cold / 혹한의 길로틴 (1303) | `Charm_Guillotine` | Timed nearby-target waves; shock can accelerate recharge. Ice and converted fire share the limit. |
| Thunder's Earring / 썬더의 귀걸이 (1239) | `Charm_ElectricEarring` | Periodic target search and projectile bursts. |
| Red Snake Eyes / 붉은 뱀의 눈 (1032) | `Charm_Meteor` | Periodic meteor/explosion; weapon actions can accelerate recharge. |
| Phoenix Wing / 봉황의 날개깃 (1214) | `Charm_FireFeather` | Periodic nearby-target projectiles. |
| Electro Chakram / 전격 차크람 (1013) | `Charm_Chakram` | Orbiting proximity collisions. |
| Echo of the Glacier / 빙하의 메아리 (1178) | `Charm_EchoOfTheGlacier` | Upgraded direct damage on periodic pulses; shared parry-triggered pulse also uses this source. |
| Snow Mountain Long-eared Bat / 설산 큰귀박쥐 (1293) | `Charm_IceBat` | Automatic owned-Frostbite target search, then two half-damage projectiles. |

Ice Bat is an artifact visual/projectile, not a hired/summoned companion
`UnitAvatar`. With a 2-second limit, its two quick impacts share the same clock:
the second half-hit is rejected; damage is not combined into the first hit.
The same applies to multiple blades or overlapping copies from one source.

Exclusions: Dark Cloud (its own interval), planets, companion-origin damage, ordinary/direct/special weapon
attacks, books, weapon/magic-trigger-only procs (Pallas, Kunai, Ice Vine,
Flame Herb Root, Frostium Ring variants, Tuning Forks), manual active artifacts,
and retaliation/dash/kill-trigger-only effects. Frost Relic (`FROST`, 얼음 무구)
armaments such as Plitvice's sword, Voluspa's spear, Blizzard Hammer, Frozen Bow
and AirSlash remain excluded, including fire conversions. **Guillotine is the
explicit requested exception** to that category. Glacier (`GLACIER`, 빙하) is a
different category, so Echo and Ice Bat remain eligible. Eternal Furnace
replenishes Solar Blade charges; it does not independently fire them.

No broad `Charm_`, `Projectile`, `None`, element or `FROSTRELIC` match is used.
All seven catalog entries require their exact ID, native `Projectile` damage
type and `None` from-type, with the player as the actual damage source. Optional
accelerators/triggers on the same eligible artifact share its clock; native
weapon hits themselves are never reclassified as that artifact.

## Impact boundary and secondary effects

The existing authoritative `BeforeHit` scope validates hostility/receipts and
rejects known cooldown repeats before native guards and ordinary hit callbacks.
The gate is read-only until positive resolved damage reaches the existing
pre-shield scaling boundary. Native misses, evasion, invulnerability and
zero-damage attempts do not spend an artifact interval. Admitted shield-absorbed
damage does. A late recheck prevents nested callbacks from admitting two hits.
An exceptional native hit conservatively retains its reservation.

This is a deliberate hard artifact cadence, separate from the damage allowance
and rapid-hit normalization. Rejected repeats do not spend damage credit or
advance hit-frequency history. An eligible hit retains the existing positive
minimum when the shared damage budget is empty. No extra damage is synthesized.

Secondary debuffs remain governed by the independent PvP DoT settings. In
particular, Chakram and Echo apply statuses outside the return value of their
direct damage call; their status applications, durations and stacks are not
suppressed by this damage control. Base Echo only applies Frostbite and has no
direct damage to limit. This is not a per-artifact cap on all subsequent DoT.
Dark Cloud already gates its whole strike upstream because its independent
callback applies Blazing Stormcloud Burn. Its dedicated interval continues to
gate the whole strike, including callbacks/FX, independently of this limiter.
Other native visual emissions remain, even when an impact is rejected.

## Shared lifecycle and performance

`CurrentBalanceWindow` is extracted from the existing balance ledger, used by
both early admission and resolved damage. It owns connection receipts, score
and participant epochs, balancing revision, source/target lives and floor IDs.
Thus casual PvP, Deathmatch, Tournament, Play Tag and Zombie share resets and
team rules. Leaving/rejoining never reuses an old connection clock. Revival,
selection/score/mode resets and floor changes retire the relevant windows.

Live numeric/toggle edits preserve the last admitted time. While balancing is
on, admitted eligible hits are recorded even with this optional limiter off;
turning it on cannot manufacture an extra immediate hit. Disabling balancing
restores native cadence, and re-enabling starts a fresh balance lifetime. An
equipment swap or projectile pool reuse cannot mint a new source clock.

Storage is a seven-element value-type array allocated lazily on the pair's first
eligible hit. A weak receipt table owns it alongside existing budgets; no new
polling, roster/inventory scans, per-projectile objects, reflection or per-hit
allocation. The 1,000-hit warmed admission/rejection probes allocate zero bytes in the
executable fixture. This is not a Unity frame-rate benchmark.

## Evidence and verification

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA summary evidence
`ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`
establishes artifact identity, not runtime behavior.

Matching ILSpy sources: 207 `Charm_*` and eight `ComboEffect_*` classes scanned
for emission, autonomous updates and targeting; candidate paths inspected with
`UnitAvatar.ApplyDamage`, `Bullet.AttackOnServer`, `BulletDestroyModule_Explode`
and the existing artifact adapters. Fresh Unity resource parsing covered 496
ItemEntity objects and exact ID strings in `resources.assets` and
`sharedassets0/1/2.assets`. Seven IDs had no collisions with excluded armaments
within that audited scope. Guillotine parent/child both serialize its ID;
Feather/Earring/Meteor bullets inherit runtime spawn identity. Meteor uses its
explosion module rather than normal direct bullet damage.

Resources SHA-256:
`906ba704cf4eefa8e300501c3b4aeefdeae91883885eb8fbe31ab754c60a3f9b`;
sharedassets0 SHA-256:
`73135788932f461054da76fde2bba1793c64748e77e9dfa3028a1ab6da47ee36`.
No native code, binaries or extracted assets are committed.

Non-deploying Release build passes with zero warnings/errors. Tests cover policy
and preset round trips, host-only commands, all match modes and live edits,
connection/run transitions, exact cadence, independent sources/players,
exclusions, guard/callback rejection, nested damage, budget exhaustion, and
Dark Cloud interval independence. Installed-game IL contracts validate emitter IDs,
shared impact routes, lifecycle and UI wiring; EN/KO catalogs pass validation.

Live host/guest gameplay and UI rendering remain unverified. Check boosted
Guillotine waves, Ice Bat double shots, Meteor explosions, Chakram proximity,
Echo debuffs and Cloud Burn with an unmodified guest, then repeat across
revive/rejoin/second run. Compare excluded weapons/books/companions and normal
enemy damage. Static coverage does not prove future/modded artifact coverage.

To extend the catalog, verify an item's native trigger, serialized identity,
damage type/from-type and collisions against excluded sources; add its fixed
slot and tests. Do not infer eligibility from its displayed name alone.
