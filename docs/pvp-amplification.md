# PvP All Damage Amplified limit

Implemented in `0.80.0`, 2026-10-21. Use this with the current
[PvP balancing](pvp-balancing.md) and [damage cadence](pvp-hit-frequency.md)
findings. This option is host-authoritative and uses the existing friendly-fire
damage and participant infrastructure; it adds no guest assets or network state.

## Controls and meaning

The **PvP Balance** tab contains an always-visible **All Damage Amplified limit** checkbox
and paired soft/hard inputs. Defaults: off, soft **50%**, hard **100%**.
English and Korean catalogs cover controls, help and status.

```text
/one friendlyfire amplification on
/one friendlyfire amplification limits 50 100
/one friendlyfire amplification off
/one friendlyfire amplification reset
/one friendlyfire status
/one save
```

PvP balancing must also be enabled. Limits accept up to two decimal places,
`0 <= soft <= hard <= 10000`. Both values are applied atomically. `off` preserves
the configured limits; `reset` restores off/50/100. Changes do not interrupt an
active match, reset K/D/A or refill the existing damage allowance. Preset v43
saves nondefault amplification policy; older presets retain off/50/100.

Values are **bonus percentage points**: 100% is a +100% contribution to native
base damage. The C-menu `FINAL_DAMAGE` display includes the normal 100% base,
so its displayed total is 100 points higher. Native character stats and that
display remain unchanged; the limit applies only when calculating an eligible
PvP hit. This is not a cap on total damage or on other amplification sources.

## Curve

For native bonus `x`, soft limit `S`, hard limit `H`, and `W = H - S`:

- `x <= S`: use `x` unchanged, including native negative costume penalties.
- `S < x < S + 2W`: let `t = (x - S) / (2W)` and use `S + W*t*(2-t)`.
- `x >= S + 2W`: use `H`.

The middle curve has slope 1 at the soft boundary and 0 at the cap. It is
monotone and smoothly meets both straight sections, with diminishing benefit
for additional investment. Equal soft/hard settings deliberately make a direct
hard cap; no division by zero occurs.

With the defaults:

| Native bonus | Effective PvP bonus |
| --- | --- |
| 25% | 25% |
| 50% | 50% |
| 75% | 71.875% |
| 100% | 87.5% |
| 125% | 96.875% |
| 150% or more | 100% |

## Shared native boundary

Installed `Assembly-CSharp.dll` SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
REA's managed member inspection exceeded its parser budget in prior research;
the matched local decompilation and installed IL contracts establish this path.
Keep those exports outside Git.

`UnitAvatar.ApplyDamage` reads `GetCustomStat(ECustomStat.AllDamageBonus)` from
the attacking avatar and adds its `NetworkLeader` bonus when present. It then
calculates `damage.damage * (combinedBonus / 100f)` and adds that contribution
alongside native companion, negotiation and elite bonuses. It does **not**
simply multiply the entire later damage result by this percentage.

`FriendlyFireAmplificationHooks` validates both reads, their shared source and
integer accumulator, and its sole damage consumption. It inserts one
`FriendlyFireRuntime.LimitAmplification(float)` call after the combined bonus
is converted to float, before division/multiplication. Companion and owner
bonuses are thus limited together, once. All remaining native arithmetic,
defenses, callbacks and the downstream PvP damage scale/softening/budget remain.
The audited native tree has no other damage-consuming AllDamageBonus reads;
other reads provide UI values, and producers add/remove the underlying stat.
Weapons, projectiles, artifacts and elemental effects reaching this shared
calculation receive the same treatment. Effects bypassing this stat gain no new
amplification or artificial reduction from the option.

The adapter requires the existing current friendly hit, a player victim,
authoritative host, active balancing, this toggle, and current connection/mode
permission. The shared mode overlay preserves the setting for casual friendly
fire, Deathmatch, Tournament, Play Tag and Zombie Survival. Nested PvE calls get
their own native scope. Self/system damage, ordinary enemies and companion/NPC
victims remain native. Unselected players and same-team/spectator pairs retain
their existing protections.

Nothing writes player stats, retains per-player amplification state, or schedules
a restore operation. Disabling either option, ending a mode, changing teams or
participants, reconnecting, or an exception cannot leave a stat adjustment behind.
Already-fired projectiles read the current policy at impact.

## Cost and validation

The hot path uses existing hit permissions, a few float operations, and a
reciprocal calculated when settings change. There are no scene scans, per-frame
updates, allocations, extra stat reads, or new network messages. Disabled and
non-PvP hits take a short return path. Existing budget/cadence history is retained.

Verification covers the approved values, monotonicity and boundary slopes,
fractional/equal limits, native penalties, large inputs, invalid settings,
host-only commands, atomic edits, old/new presets, match overlays and cleanup.
Executable Harmony fixtures exercise the actual inserted adapter, companion
aggregation, nested PvE, live toggles, stale connections and zero allocations
across 1,000 warmed balanced hits. Installed-game checks verify exact native IL,
branch/exception metadata, compatibility with the complete damage pipeline,
and rejection of changed signatures, arithmetic or ambiguous consumers.

These checks do not establish live host/guest balance or visual layout. No
deployment or game launch is part of this change. Live follow-up should compare
the same player's attacks against a player and a normal monster with native
bonuses below, inside and above the curve, then disable the toggle mid-session.

Release verification: non-deploying build with zero warnings/errors; 8,691 combat
hook checks, 13,796 runtime integration checks, 160 dedicated policy/preset
checks, 4,327 bundled catalog checks, panel and localization/lifecycle suites,
and the portable/installed-game contracts passed. The binary's 34 Harmony API
references also passed against co-installed HarmonyX 2.9.0.0. These are executable
fixtures and metadata/IL contracts, not a live multiplayer test.
