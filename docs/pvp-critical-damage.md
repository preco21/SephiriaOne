# PvP Critical Damage limit

Implemented in `0.102.0`, 2026-10-29, using the existing
[All Damage Amplified curve](pvp-amplification.md) and balanced friendly-fire
hit scope. Only the host needs the addon. No new guest state/assets/messages
are introduced; live guest gameplay has not been verified for this release.

## Controls and units

**PvP 밸런싱 (기본) / PvP Balance** includes **치명타 피해 제한 / Critical Damage
limit** with paired soft/hard inputs. Defaults: **off**, soft **30**, hard **70**.
Both this toggle and PvP balancing must be on.

```text
/one friendlyfire critical-damage on
/one friendlyfire critical-damage limits 30 70
/one friendlyfire critical-damage off
/one friendlyfire critical-damage reset
/one friendlyfire status
/one save
```

Accepts `0 <= soft <= hard <= 10000`, with at most two decimals. Edits are
atomic; off keeps values, reset restores off/30/70. Changes do not stop matches,
reset K/D/A, clear hit history or refill budgets. Preset v57 stores nondefault
policy; older presets inherit off/30/70.

The user chose to preserve the **native +50% critical bonus** and limit only
the additional bonus. The native C-menu displays `50 + CriticalDamageBonus`:
default 50% means a 1.5× critical hit. These inputs are additional bonus points,
not that displayed total. Player stats, the C-menu and attack snapshots stay
native. Per-attack magic/weapon bonuses and companion contribution participate
in the combined additional bonus at impact.

## Shared curve

For additional bonus `x`, soft `S`, hard `H`, `W = H - S`:

- `x <= S`: unchanged, including native negative values.
- `S < x < S + 2W`: `t = (x - S) / (2W)`, result `S + W*t*(2-t)`.
- `x >= S + 2W`: `H`.

This reuses `PvpStatLimit`. Hard limits the **effective** bonus; it is not an
immediate clamp when unadjusted bonus first reaches that number. Equal limits
produce a direct cap. Reciprocals are cached when settings change.

| Additional native bonus | Effective additional bonus | Ordinary critical multiplier |
| --- | --- | --- |
| 0 | 0 | 1.5× |
| 30 | 30 | 1.8× |
| 50 | 47.5 | 1.975× |
| 70 | 60 | 2.1× |
| 90 | 67.5 | 2.175× |
| 110 or more | 70 | 2.2× |

This excludes other modifiers. Separate special critical multipliers and
execution's doubled contribution remain; it is not a universal 2.2× final
damage cap. Critical Chance and noncritical hits are unchanged.

## Native boundary and scope

Installed `Assembly-CSharp.dll` SHA-256:
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA identity evidence `ev_c3733c62f61d0c4e651bfc15807d5dc53d2140cdbe2e223042a8a4df533a43a1`;
summary `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`.
Behavior was traced in matching cached decompilation and verified with installed
IL. Game source and private logs remain outside Git.

`DamageInstance.Set` initializes `criticalDamageRate = 50 + CriticalDamageBonus`,
then adds magic/weapon bonuses; `WEAPONCRITICALDAMAGEAMPLIFY` is already baked
in. `UnitAvatar.ApplyDamage` copies that rate, adds the truncated leader bonus
(`CriticalDamageBonus * FOLLOWERCRITICALCONTRIBUTE / 100`), optionally applies
`criticalDamageMultiplier` with `Mathf.RoundToInt`, and uses the resulting rate
for ordinary critical/execution damage. `AvatarStatsHooker` confirms the C-menu
base via `CRITICAL_DAMAGE_RATE`.

The hook creates one float local after leader aggregation, before the special
multiplier. Its adapter returns `50 + limit(nativeRate - 50)` for authoritative,
permitted, balanced PvP hits against players. Three consumers use that local:
multiplier, execution, ordinary critical. Special-multiplier integer rounding
remains native; without it, fractional curve output is retained.

No player stat or damage-instance field is overwritten. Lake Spirit's separate
multiplier and Fire Needle's post-hit projectile count remain native. Kunai's
independent base-damage scaling from critical stats is not rewritten. Native
signed penalties, callbacks, shields and downstream PvP scaling/budgets remain.

Shared guards cover casual friendly fire, Deathmatch, Tournament, Play Tag and
Zombie Survival. PvE, system/self damage, NPC/companion victims and disallowed
pairs do not acquire this limit. Nested hits retain their own scope. No stat
restoration is needed on toggles, session/role changes or reconnection. Existing
projectiles retain their native snapshot but use current limits at impact.

## Verification

Executable tests cover base/soft/hard/negative values, fractional precision,
execution, special multipliers (including zero and rounding), immutable attack
fields, nested PvE, live permissions and exceptions. No scanning, per-frame work
or networking was added: **0 bytes across 1,000 warmed balanced fixture hits**.

Installed contracts validate aggregation, multiplier/rounding, both consumers,
preserved native IL/branch metadata, drift/duplicate rejection and shared pipeline
composition. Runtime tests exercise settings edits in all match types without
score/budget resets, presets, restart/reconnect and unavailable-hook recovery.
Release build, combat/runtime/native/catalog suites and HarmonyX 2.9 API checks
pass. These are fixtures/static contracts, not live Unity or UI rendering.

No deployment. Live follow-up: compare critical/noncritical hits against players
and monsters, then change/off/reset limits mid-match.
