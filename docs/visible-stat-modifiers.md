# C-menu stat modifiers

Version `0.36.0` adds **17 modifiers**, bringing `/stats` to 27 supported stats.
Each new entry is present in the installed game's `C` status panel, including
conditional Special-tab rows and the crossbow-specific reload row. Hidden
effects are not exposed. Existing stat meanings and limits remain unchanged.

Open `/one ui` → **Stats**, or use `/stats list` for command names, localized menu
labels, units and bounds. All new modifiers support set/add/subtract, `xN`, reset
and `/one save`, using the existing host-only settings system and native state
synchronization for unmodified guests.

```text
/stats hpsteal +3
/stats grimoiredamage x2
/stats dashrecovery set 150
/stats ignoredefense 25
/stats hpsteal reset
/stats reset
/one save
```

## New supported values

| Command name | C-menu stat | Units / meaning | Allowed result |
| --- | --- | --- | --- |
| `toughness` | Toughness | Fixed damage reduction per hit | 0–10000 |
| `dashrecovery` | Dash Recovery Speed | Total %, 100 = normal | 1–1000 |
| `expdrop` | Experience Drop | Total %, 100 = normal | 0–10000 |
| `leafdrop` | Leaf Drop | Total %, 100 = normal | 0–10000 |
| `thorns` | Thorns | Reflected damage as % of defense | 0–10000 |
| `normaldamage` | Normal Attack Damage | Total %, 100 = normal | 0–10000 |
| `dashdamage` | Dash Attack Damage | Total %, 100 = normal | 0–10000 |
| `specialdamage` | Special Attack Damage | Total %, 100 = normal | 0–10000 |
| `weapondamage` | Weapon Damage | Total %, 100 = normal | 0–10000 |
| `grimoiredamage` | Grimoire Damage | Total %, 100 = normal | 0–10000 |
| `alldamage` | Universal Damage Boost | Total %, 100 = normal | 0–10000 |
| `hpsteal` | Life Steal | Points; 1 point = 0.1% | 0–10000 |
| `mpsteal` | MP Steal | Points; 1 point = 0.1% | 0–10000 |
| `ignoredefense` | Ignore Defense | Defense ignored % | 0–100 |
| `debuffduration` | Debuff Duration | Bonus %, as displayed | 0–10000 |
| `debuffdamage` | Debuff Damage | Bonus %, as displayed | 0–10000 |
| `crossbowreload` | Crossbow Reload Speed | Total %, 100 = normal | 1–1000 |

Set/add/subtract amounts use whole display units for these entries. Multipliers
retain the shared fractional-factor syntax (up to two decimals). Native percent
rows with a 100% baseline use that displayed total: native grimoire damage 130%
with x2 targets 260%, not 160%. HP/MP steal use points rather than percent: native
5 points with x2 targets 10 points (1% steal). A zero native baseline multiplied
by any factor stays zero; use `+N` to add points.

`+10` then `+5` retains an offset of +15 from each player's own native value.
Repeating x2 never compounds; a delta after set/xN starts a new native offset.
Invalid multiplier results preserve the exact native stat and remove only the
addon contribution. The factor remains retained for compatible later inputs.
Native equipment, costume, amplifier and passive changes use the existing
relative-stat reconciler. Bat costume HP-steal reduction changes the baseline
observed by `/stats hpsteal`; the two features retain separate ownership.

Aliases include `lifesteal`, `mp-steal`, `magicdamage`, `penetration`, `basicdamage`,
`crossbowreloadspeed`, `expgain` and `moneydrop`. Presets store canonical names.
Only presets containing new stat settings require v17. Resetting those settings
returns serialization to the lowest schema needed by the remaining features.

## Eligibility and native audit

The supplied [status reference](https://www.sephiria.tools/statuses) is useful for
names and units but includes hidden/internal effects. The supplied
[Namu page](https://en.namu.wiki/w/%EC%84%B8%ED%94%BC%EB%A6%AC%EC%95%84#s-4.1)
could not be retrieved during this audit. Eligibility was confirmed directly
against installed game assets and code on 2026-10-09.

The `level2` scene has `UI_StatsPanel` component **22283**, under
`[UI] Panels/StatsPanel`. Its serialized `statElements`, `categories`,
`conditionalCategories` and `weaponCategories` reference the rows below. Each
row component and its GameObject are enabled. Category visibility is controlled
by `UI_StatsPanel.OnOpened/Refresh`; new modifiers do not force categories open.

| Command | Native custom-stat key | Menu hook ID | level2 component |
| --- | --- | --- | --- |
| `toughness` | `TOUGHNESS` | `TOUGHNESS` | 21769 |
| `dashrecovery` | `DASHRECOVERY` | `DASH_RECOVERY_SPEED` | 20956 |
| `expdrop` | `EXPDROP` | `EXP_DROP` | 23287 |
| `leafdrop` | `MONEYDROP` | `LEAF_DROP` | 23281 |
| `thorns` | `THORNS` | `THORNS` | 21475 |
| `normaldamage` | `BASICATTACKDAMAGEBONUS` | `BASIC_ATTACK_DAMAGE` | 22011 |
| `dashdamage` | `DASHATTACKDAMAGEBONUS` | `DASH_ATTACK_DAMAGE` | 21064 |
| `specialdamage` | `SPECIALATTACKDAMAGEBONUS` | `SPECIAL_ATTACK_DAMAGE` | 20786 |
| `weapondamage` | `FINALWEAPONDAMAGE` | `FINAL_WEAPONDAMAGE` | 21263 |
| `grimoiredamage` | `MAGICDAMAGEBONUS` | `MAGIC_DAMAGE_BONUS` | 24375 |
| `alldamage` | `ALLDAMAGEBONUS` | `FINAL_DAMAGE` | 24488 |
| `hpsteal` | `HPSTEAL` | `HP_STEAL` | 23852 |
| `mpsteal` | `MPSTEAL` | `MP_STEAL` | 20878 |
| `ignoredefense` | `IGNOREDEFENSE` | `IGNORE_DEFENSE` | 22823 |
| `debuffduration` | `DEBUFFDURATION` | `DEBUFFDURATION` | 21110 |
| `debuffdamage` | `DEBUFFDAMAGE` | `DEBUFFDAMAGE` | 21971 |
| `crossbowreload` | `CROSSBOWRELOADSPEED` | `CROSSBOWRELOADSPEED` | 22470 |

`AvatarStatsHooker.HookStat` supplies the display units and offsets. The crossbow
subclass returns reload percentage unchanged (capacity and fire rate use other
calculations). Debuff rows use the hooker's default raw-key path with a serialized
`%` suffix. Every exposed key uses `UnitAvatar.GetRawStatUnsafe`: integer
raw-plus-bonus and amplification, followed by float division and truncation.
This matches the existing `StatPlanner`; no new game hooks are needed.

## Deliberate exclusions

- HP regeneration, magic critical chance/damage, potion bonuses, dash count,
  weapon range and other reference/database entries absent from this menu.
- Storm Cloud / Flame Ground assets exist in the scene, but the native
  `AvatarStatsHooker.HasCategory` currently returns false for those categories.
- Physical/fire/cold/lightning damage are visible, but their native getter routes
  through `GetConvertedElementalDamage`. Conversion can cap a source at 20 and
  add damage from another element. These require a coupled calculation and are
  not exposed through the independent stat planner.
- HP/MP maxima and movement use direct native fields; level-up healing also
  includes hard-mode adjustments; crossbow capacity/magazines add weapon-specific
  defaults. These visible values need separate baseline handling. Crossbow firing
  speed already depends on the existing `attackspeed` setting.

This update intentionally does not enumerate all `StatusDatabase` or `ECustomStat`
entries. The explicit catalog is the shared source for command names, panel labels,
menu identities, units, validation limits and preset compatibility.

## Verification and performance

Portable tests exercise the allowlist, aliases, integer units, per-character
native multipliers, negative native fallback, reset, v17/older presets and mixed
feature persistence. Runtime fixtures exercise every new stat through repeated
commands, native raw/amplifier changes, same-ID reconnects, second runs, saved
reloads, host authorization, and interaction with Bat costume HP-steal reduction.
Installed-game IL checks verify menu readers, display offsets, raw-vs-converted
stat routing, and catalog-driven panel labels. EN/KO catalog tests cover all labels
and units. Builds use `DeployMod=false`.

No additional polling or state-write path was introduced. Reconciliation still
tracks only active relative settings. The five-player fixture with all 27 stats
active allocates **0 bytes per unchanged synchronization tick**. Pre-sized
snapshot dictionaries keep compact panel capture at about **25.4 KB/refresh**,
within the existing 25 KiB test budget, compared with 23.2 KB for the original
10-stat catalog. Full diagnostics grow with the additional reported values and
are still requested only on Status/chat paths. Fixture timings do not establish
in-game frame rates.

No live Unity/UI/multiplayer test or deployment was performed. After manual
installation, check Stats in EN/KO, matching `C` values, conditional rows with
bonuses, crossbow reload with a crossbow equipped, a stock guest reconnect,
costume switches, native menu stat edits, and a second run. The native menu does
not subscribe to amplifier-only updates; reopening it may be needed if the addon
has no raw contribution change to trigger a refresh.
