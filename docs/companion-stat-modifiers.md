# Companion stat modifiers (0.51.0)

Open `/one ui` → **Stats** and choose a Companion / 동료 entry. All six support
set, add/subtract, per-character `xN`, reset and `/one save`. Only the host changes
them; unmodified guests use the game's replicated owner stats.

| Command | Label | Units | Allowed target |
| --- | --- | --- | --- |
| `companionattackspeed` | Companion attack speed / 동료 공격 속도 | Total %, 100 = normal owner factor | 1–1000 |
| `companiondamage` | Companion damage / 동료 피해량 | Total %, 100 = normal owner factor | 0–10000 |
| `companiondefense` | Companion defense / 동료 방어력 | Extra defense points | 0–10000 |
| `companioncritical` | Companion critical chance / 동료 치명타 확률 | Additional percentage points, two decimals | 0–100 |
| `companioncriticalshare` | Companion critical inheritance / 동료 치명타 능력치 계승 | Percentage of owner's critical stats inherited | 0–100 |
| `companionrevive` | Companion revival speed / 동료 부활 속도 | Total timer speed %, 100 = normal | 1–1000 |

```text
/stats companionattackspeed x2
/stats companiondamage +25
/stats companiondefense set 10
/stats companioncritical set 12.25
/stats companioncriticalshare set 50
/stats companionrevive set 200
/stats companionattackspeed reset
/one save
```

Settings modify owner bonuses, not each creature's complete stats. Native attacks,
artifact levels and companions' own bonuses still participate. Native owner
speed 120% with x2 targets 240% (`FOLLOWERATTACKSPEED = 140`); this does not multiply
separate follower speed additions. Revival speed 200% advances supported native
revival/summon timers twice as fast, without overriding their base durations.

Defense, critical chance and inheritance have zero-based bonuses: zero times N
stays zero; use `+N` or `set N` to introduce a bonus. Relative offsets accumulate
from each owner's native values; repeated xN replaces the factor. Invalid native
multiplier targets preserve the exact native value. Reset removes only addon
contributions. Existing stat changes, re-entry and new runs use shared reconciliation.

Aliases: `followerattackspeed`, `followerdamage`, `followerdefense`,
`followercritical`, `followercriticalcontribute`, `followerrevive`,
`companioncriticalinheritance`, `companionrevivespeed`. Presets store canonical
names. Active companion settings require v21; older schemas reject these names.
Reset returns serialization to the lowest schema needed by remaining settings.

## Native scope and evidence

This is the requested companion extension to the earlier [C-menu allowlist](visible-stat-modifiers.md).
These are native artifact/status bonuses, but not C-menu rows: all 70
`UI_StatusTooltipOpener` components in installed `level2` were rechecked, with no
`FOLLOWER`/`COMPANION` stat identity. No native or guest UI rows are added.

Inspected 2026-10-12; installed assembly SHA-256 still matches
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Enabled/displayed `resources.assets` StatusEntity definitions:

| Asset ID | Native key | Entity ID / display |
| --- | --- | --- |
| 30684 | `FOLLOWERATTACKSPEED` | `FOLLOWER_ATTACK_SPEED`, bonus % |
| 30685 | `FOLLOWERCRITICAL` | `FOLLOWER_CRITICAL`, raw / 100, % |
| 30686 | `FOLLOWERCRITICALCONTRIBUTE` | `FOLLOWER_CRITICAL_CONTRIBUTE`, % |
| 30687 | `FOLLOWERDAMAGE` | `FOLLOWER_DAMAGE`, bonus % |
| 30688 | `FOLLOWERDEFENSE` | `FOLLOWER_DEFENSE`, points |
| 30689 | `FOLLOWERREVIVE` | `FOLLOWER_REVIVE`, bonus speed % |

All use `StatusInstance_Custom` and normal owner custom-stat arithmetic. The
addon displays speed/damage/revival with a 100% offset, consistent with existing
speed/damage controls. Critical retains native hundredths of percentage points.

- `UnitAvatar.GetBonusAttackSpeed` adds live leader `FOLLOWERATTACKSPEED`; native
  AIs consume this for attack timing.
- `UnitAvatar.ApplyDamage` reads attacker's leader follower damage/critical
  bonuses and defender's leader follower defense.
- Critical inheritance copies the configured fraction of owner `CRITICAL` and
  `CRITICALDAMAGEBONUS`, excluding base 50% critical damage. Native execution
  eligibility inheritance and debug logs for nonzero inherited values remain.
- `Charm_LeadNPC`, `Charm_SummonUnit` and `Charm_MiniBallista.OnUpdate` advance
  timers by `deltaTime * (1 + FOLLOWERREVIVE / 100)`.

Companion HP/movement are artifact/prefab-specific fields, not additional global
owner bonuses in these definitions. Existing `alldamage`, `truedamage` and
`ignoredefense` already contribute through leader reads; no duplicate controls.

## Architecture and verification

`StatCatalog` drives the dropdown, commands, bounds, labels and contribution
markers. `IsCompanion` identifies this group and its v21 requirement without
claiming C-menu visibility. Existing stat/session services apply owner state;
native reads pick it up for existing/new companions and current leader assignments.
No additional follower subscriptions, polling, caches, writes or network protocol.

Passed: 250 companion policy/preset checks; 1,905 runtime checks plus 69 Bat
checks, covering every new stat through raw/amplifier changes, same-ID re-entry,
second runs, save/reload, relative accumulation, reset and host authorization.
Installed-code contracts verify leader reads, critical units, three revival timer
paths and replicated dictionaries. Catalog coverage: 3,322 checks, including
older custom-file fallback. Debug/Release builds: zero warnings/errors.

Five-player/all-settings fixture: unchanged synchronization 0 bytes/tick; Stats
tab 9,448 bytes/refresh; Presets 9,216; full diagnostics 138,640. Actual tab budgets
are unchanged. The compatibility all-sections snapshot adds six values/settings
and measures 27,616 bytes; its explicit 33-stat budget is now 28 KiB, formerly
25 KiB. Fixture timing is not a live Unity performance claim.

No deployment or live multiplayer/UI test. Verify dropdown layout, native
companion timing/damage, summons after configuration, leader changes and stock
guests in the game. All verification builds used `-p:DeployMod=false`.
