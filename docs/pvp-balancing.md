# Optional PvP balancing

Added in `0.52.0`, 2026-10-13. Host-only installation; the existing native damage
and FX replication continues to serve stock guests. Default: **off**.

Enable **Combat → PvP balancing** (`PvP 밸런스 보정`) or use:

```text
/one friendlyfire balance on
/one friendlyfire balance off
/one friendlyfire status
/one save
```

The preference is independent of the friendly-fire master switch. It affects
current permitted player pairs in casual friendly fire, Deathmatch and Tournament
only while effective friendly fire is active. Empty selection means everyone;
nonempty selection and Tournament's current pair keep their existing rules.
Changing balancing does not stop a match or clear K/D/A. Touching the master
toggle still uses the existing stop/recovery semantics.

## Model and trade-offs

The user selected **per-hit softening only**, with no sustained-damage budget or
rolling DPS limit. Native stat rewriting would need to track equipment, costumes,
buffs, passives, preset edits and ownership, and could affect PvE. Roster-relative
normalization would change combat when players join/leave. A hard damage cap
would make many large hits identical. The chosen smooth curve uses the actual
victim's maximum HP and the fully resolved incoming damage instead.

`FriendlyFireRuntime.Scale` already executes after the native critical, elemental,
defense, true-damage and toughness calculation, before regular and MP shields.
The host's damage percentage applies first. For that result `D` and actual native
maximum HP `H`, the soft knee is `K = 0.05H`, and the ceiling is `C = 0.20H`:

```text
D <= K: output = D
D > K:  output = K + (C-K) / (1 + (C-K)/(D-K))
```

This form avoids intermediate overflow. It is continuous and increasing, never
increases damage, and gradually approaches 20% of maximum HP per hit. Large hits
remain stronger than smaller ones. No assumption depends on 100–200 HP.

| Scaled native hit, as a fraction of maximum HP | Balanced hit |
| --- | --- |
| 1% | 1% |
| 5% | 5% |
| 10% | 8.75% |
| 20% | 12.5% |
| 100% | about 17.95% |
| Extremely large | approaches 20% |

Shields, MP shields, super armor, invulnerability, guard/parry, additional lives,
damage-triggered mechanics and later native presentation retain their own rules.
Reflection, audited artifact procs, damage-over-time and owned companion damage
to player victims use the same resolved-hit boundary. Damage to monsters, normal
incoming monster damage, self/system damage and non-player ally targets retain
their previous paths. There is no global faction or targeting change.

Attack rates, number of projectiles, proc conditions and debuff duration/stacking
are not normalized. Rapid attacks and focus fire can still kill quickly. Strong
healing or defensive builds may remain hard to kill. This reduces extreme hit
power, not every possible build advantage. Small hits are not buffed, and very
low host damage percentages can still produce negligible damage; try 100% as a
starting point when evaluating balancing. The setting never changes that slider
automatically or introduces a minimum damage floor.

## Dark Cloud

The existing 0.5-second attacker/player-pair strike limit is now part of this
toggle. **When balancing is off, that limit is off too**, even if friendly fire
is on. All other Dark Cloud behavior remains as recorded in the
[artifact guide](friendly-fire-artifacts.md#dark-cloud-player-strike-limit-0512-2026-10-13).

When on, the whole repeated strike is skipped (damage, strike-triggered burn and
FX), including queued strikes after the owner dies. Existing cloud costs and
timers remain native. Turning balancing off/on with no intervening strikes changes
`CombatBalanceRevision`, so a previous cooldown cannot be reused. Existing weak
connection receipts, participant/KDA epochs and floor checks handle disconnects,
rejoins, selections, match rounds, restarts and resets without per-frame work.

## State, persistence and restoration

The balancing layer writes **no player stats, health limits, native bonuses,
inventory or pooled DamageInstance values**. Actual `MaxHp` is read on every
relevant hit; changing a costume, native menu, item or buff needs no resync. Invalid
nonpositive/nonfinite maximum HP produces zero balanced damage rather than invalid
HP/MP writes. The existing native integer-safety ceiling remains in force.

There are no modified stats to restore on off, end, cancellation, interruption,
reset, disconnect or unload. Match cleanup disables effective friendly fire as
before; the retained balance preference alone cannot alter damage. `/one
friendlyfire off` retains the preference; `/one friendlyfire reset` restores it
to off along with the other combat defaults. New sessions use defaults unless a
preset was explicitly saved. Enabled balancing uses preset v22; earlier versions
still parse and default balancing to off. Malformed/duplicate rows fail atomically.

## Verification and limits

Executable tests cover scale invariance across maximum HP values, monotonicity,
finite extremes, weak/strong hits, fractional host percentages, shields, defenses,
reflection, companion damage, debuff/triggered damage, invalid input, no mutation,
unrestricted same-frame repeated hits, selection exclusions and live changes.
Runtime tests cover saved presets, restart/rejoin, scope replacement, host-only
commands, unavailable-hook recovery, both match overlays and retained K/D/A.
Native compatibility tests retain the pre-shield scale boundary and check native
maximum-HP reads and shared UI command wiring. Warmed arithmetic and cloud checks
allocate no per-hit memory in the fixture; no new polling or network path is added.

Verification on 2026-10-13:

- Debug and Release builds with `-p:DeployMod=false`: no warnings or errors.
- Combat executable: 1,185 checks passed, including the new curve and Cloud gates.
- Runtime executable: 1,920 session/runtime checks and 69 Bat checks passed.
- Portable/native suite: all checks passed, including 194 friendly-fire policy
  checks, 3,337 catalog checks, installed-game contracts and 28 compiled Harmony
  API references against co-installed HarmonyX `2.9.0.0` (metadata audit).
- Performance harness: unchanged five-player reconciliation still allocates
  zero bytes per tick with no settings or all settings active.
- Independent code review found no actionable issues.

These tests do not establish a live match's balance, latency or visual layout.
Manual checks should compare slow/rapid weapons, debuff builds, shields, low/high
HP costumes and multiple stock guests. No deployment or game launch performed.
