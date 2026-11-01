# Configurable PvP Evasion

Added in **0.109.0**, 2026-11-02. Replaces the fixed 50% stat-based dodge cap
introduced in 0.60.4. Approved defaults: **enabled, soft 30%, hard 50%**.
The independent toggle still requires PvP balancing and an eligible hostile hit.

## Controls and units

**PvP 밸런싱 (기본) / PvP Balance** contains **회피 확률 제한 / Evasion chance
limit**, soft/hard chance inputs, Apply and Reset. These are effective dodge
probabilities, not the raw Evasion stat or an additional bonus.

```text
/one friendlyfire evasion on
/one friendlyfire evasion limits 30 50
/one friendlyfire evasion off
/one friendlyfire evasion reset
/one friendlyfire status
/one save
```

Limits accept `0 <= soft <= hard <= 100`, with up to two decimals. Off preserves
tuning and restores native chance; reset restores on/30/50. Setting both limits
to the same value gives a strict cap. Both zero suppress only stat-based dodge.
Hosts can intentionally choose a higher hard cap, up to 100%.

The exact shared `PvpStatLimit` curve used by All Damage Amplified applies before
attacker anti-Evasion. Below soft, chance stays native. Between soft and native
`soft + 2*(hard-soft)`, the C1 curve increases with diminishing returns; its
effective output never exceeds hard. With default settings:

| Native stat-derived dodge chance | Effective PvP chance |
| --- | --- |
| 20% | 20% |
| 30% | 30% |
| 40% | 38.75% |
| 50% | 45% |
| 60% | 48.75% |
| 70% or more | 50% |

For example, native 50% becomes 45%; an attacker with 50% anti-Evasion then
reduces that to 22.5%. No additional random rolls are introduced.

## Native boundary and lifecycle

`FriendlyFireRuntime.EvasionChance` adapts only the stat-derived probability
inside the existing host `UnitAvatar.ApplyDamage` patch. Native chance calculation,
input clamp, anti-Evasion, roll comparison, callbacks and separate guaranteed-item
dodge branches remain intact. The installed public game calculates chance inline;
the audited 1.1.0 extracted `GetEvasionChancePercent` path also uses this adapter,
after its signed rate modifier and before attacker anti-Evasion.

No player stats, C-menu values, HP, equipment, RNG state or saved native values
are changed. Monster-to-player and player-to-monster hits stay native even while
PvP is active. Owned companion attacks against eligible player opponents share
the limiter; companion victims retain native Evasion. Native exclusions such as
MP shielding, fall damage and elemental-effect damage remain native. This does
not change the separate guaranteed-dodge artifact or counter-window policies.

Shared friendly-fire admission covers casual PvP, Deathmatch, Tournament's live
pair, Play Tag and opposing Zombie Survival factions. Current selection, receipts
and match state are checked at hit time. Disabling the limiter/balancing/PvP or
ending the match immediately removes this adjustment without a stat-restore pass.
No retained Evasion state can leak across reconnects or run transitions.

Edits use the shared host settings path, remain possible with loading guests or
unrelated stat-write faults, and do not stop matches, reset scores or refill
damage budgets. New sparse **preset v60** persists nondefault Evasion policy;
older presets inherit the approved on/30/50 defaults. Invalid or duplicate fields
reject the entire preset. Off/reset remain available after compatibility failure.

## Performance and compatibility

The existing per-hit adapter now evaluates the shared cached scalar curve.
There are no additional per-frame jobs, entity scans, stat writes, RPCs or guest
assets. The softened-hit fixture measured **0 allocated bytes over 1,000 warmed
hits**. Guests use native hit results; no Evasion-specific client install is needed
for this host-authoritative boundary on the inspected public build. Game/mod-loader
version requirements are separate from this transport finding.

Validation covers settings/curve/preset migration, live command changes in all
four modes, participant and connection transitions, native RNG/callback behavior,
off restoration, nested PvE, exception cleanup, anti-Evasion ordering, both public
and beta-style chance formulas, UI draft bindings and EN/KO catalogs. Installed
native IL guards and HarmonyX 2.9 compatibility checks passed. A regression using
Evasion 5000 and roll 46 failed under the old fixed cap and passed with the curve.

This is automated fixture/contract verification, not live host/guest gameplay or
Unity UI rendering. Nothing was deployed. Native boundary history and evidence:
[original Evasion audit](pvp-balancing.md#evasion-cap-0604-2026-10-17),
[1.1.0 combat compatibility](beta-110-combat.md),
[shared stat curve](pvp-critical-damage.md#shared-curve).

Release validation: 18,401 runtime/session checks plus 69 Bat checks, 15,840
executable friendly-fire checks, 10,180 Evasion policy/curve/preset checks and
5,090 catalog checks passed, along with installed native/UI contracts and the
41-reference HarmonyX API audit. Release build: zero warnings/errors. Independent
review identified and fixed a policy-only UI readiness mismatch; loading/fault
fixtures and UI contracts cover the correction.
