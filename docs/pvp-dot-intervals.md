# Independent PvP damage intervals

Expanded in `0.68.0`, 2026-10-20. Use **Combat → DoT intervals**
(`전투 → 지속 피해 간격`), with the scrollbar or mouse wheel to reach all nine
controls. Each setting accepts **0.05..10 seconds**, up to two decimals, and has
its own command/reset. These controls apply only to permitted player pairs while
friendly fire **and PvP balancing** are active, across casual combat, Deathmatch,
Tournament, Play Tag and Zombie Survival. Native PvE timing is unchanged.

| Control / command key | Default | Native coverage |
| --- | --- | --- |
| Burn / `burn` | 0.9s | `CharacterDebuff_Burn.tickTimer`, including blue/evolved variants. |
| Plasma Burn / `plasma-burn` | 0.75s | `CharacterDebuff_Plasma.tickTimer`, including blue/evolved variants. Separate from Plasma shock. |
| Frostbite / `frostbite` | 0.75s | `CharacterDebuff_Frostbite.frostDamageTimer`; still requires native `FROSTBITEDAMAGE`. |
| Poison / `poison` | 0.75s | `CharacterDebuff_Poison.poisonTickTimer`; system poison excluded. |
| Wound / `wound` | 1.33s | `CharacterDebuff_Wound.tickTimer`; approximately the previous 1/0.75 cadence. |
| Lightning / Shock / `lightning` | 0.25s | Electric and Plasma's distinct shock component, including the serialized `Debuff_Plasma` variant marked `DEBUFF_ELECTRIC`. `shock` is a command alias. |
| Freeze damage / `freeze` | 0.25s | `Debuff_Freeze` application/stack damage. Native stun is preserved. |
| Chim damage / `chim` | 0.25s | `Debuff_Chim` delayed expiry hit. No new periodic damage or change to its charge/lifetime. |
| Dark Cloud / `dark-cloud` | 0.5s | Native `ComboEffect_DarkCloud.FireLightning`, including ice variants and attached burn callbacks. |

```text
/one friendlyfire dot lightning 0.75
/one friendlyfire dot dark-cloud 1
/one friendlyfire dot wound 1.5
/one friendlyfire dot lightning reset
/one friendlyfire status
/one save
```

The original four defaults are unchanged. `reset` restores only the named effect;
full friendly-fire reset restores all nine. Lower intervals admit more frequent
hits, but do not invent additional native attacks: pulse/Dark Cloud controls are
minimum gaps, and remain limited by native triggers and the shared damage budget.
The [two-second Burn-family lifetime cap](pvp-balancing.md#burn-duration-cap-0592-2026-10-16)
still includes both Burn and Plasma and their variants. Without a refresh, an
interval of two seconds or longer may never tick before expiry.

## Shared policy and persistence

`PvpDotSettings` extends the existing immutable settings, snapshot, command,
status, UI and preset flow. No new per-player configuration cache is introduced.
Edits take effect during active effects/matches without resetting scores, roles,
participants, matches, damage budgets or pulse timestamps. Rejoins and fresh runs
read current authoritative settings. Commands and the UI share host validation;
the English and Korean catalogs cover all labels, help and status output.

Nondefault new controls require preset **v34**, stored as
`friendlyfire dot-<effect> N`. Existing four-control-only saves still use v28;
higher versions remain selected when other features require them. Versions
v1..33 inherit the five new defaults and reject new-control rows. Existing
explicit values remain intact; omitted rows and per-effect resets use the current
default. The `shock` alias saves canonically as `dot-lightning` and is not a preset
key. Invalid, duplicated, noncanonical or out-of-range rows reject atomically.
Runtime clocks, victims, connection identities and roles never persist.

## Timing boundaries

The five periodic controls wrap only their audited native `Timer.Update` call.
`UpdateDebuffTick` checks server authority and current permitted player pairs,
then advances the same timer by
`Time.deltaTime * timer.time / configuredSeconds`. Native tick length/progress
storage, reset-on-tick and active-once semantics remain owned by the game.
BURNSPEED cannot shorten the configured Burn/Plasma seconds. Half-complete tick
progress remains half-complete after a live edit. Stalled frames retain the
native one-tick behavior, without catch-up bursts. No timer/prefab/stat writes or
restoration journal is needed; disabling balancing resumes native timing.

Lightning, Freeze and Chim instead have independent **per-attacker/victim** pulse
clocks in the existing `BalanceWindow`. A successful admission reserves the clock
before native callbacks. Rejected repeats resolve to zero; they are not queued.
Zero-charge and budget-exhausted attempts do not reserve a new interval. Native
FX, stacks, expiry, charge and Freeze stun continue normally. Electric and Plasma
shock intentionally share the same Lightning clock, identified by the native shock
flag (or the original `Debuff_Electric` ID);
companion pulses use their owner's clock. Direct lightning/Bolt magic is not a
shock debuff and does not share this clock. Changing the interval compares against
the last admitted pulse; rejected attempts and sibling edits do not move it.

Dark Cloud uses its existing separate owner/victim strike window. It checks its
current configured interval **before** the whole strike, including damage,
strike-triggered burn and FX. Native cloud costs and scheduling remain unchanged;
reducing the interval does not force extra bolts. New cloud objects owned by the
same player cannot bypass the window. Changing its interval preserves the last
strike, and changing the Lightning setting does not alter Dark Cloud.

These paths retain shared connection receipts, participant/KDA epochs, floor and
balance revisions; pulse and Dark Cloud clocks also follow the existing death/revival budget
lifetime. The audit fixed Dark Cloud carrying long intervals across revival by
sharing the existing actor life generation, including strikes that emit no damage.
Queued strikes from an owner who is still dead keep their existing limit. Disconnect/rejoin and reset cannot inherit stale permissions. They do
not add per-frame scans, custom RPCs or guest-side addon state. Native damage/FX
replication continues to serve stock clients. Normal enemies, self/system
poison and unowned effects retain native timing; the separate hostility guard
continues rejecting damage to protected/nonparticipating players.

## Debuff and periodic-damage audit

The installed assembly has **11 concrete `CharacterDebuff` subclasses**. Five are
periodic controls, Electric/Freeze/Chim are pulse controls, and these three do not
have an independently timed combat damage hit:

- `CharacterDebuff_MagicWound`: amplifies incoming magic via attack callbacks.
- `CharacterDebuff_StatusInstance`: applies status modifiers.
- `CharacterDebuff_QliphothDeath`: scripted countdown/death, not ordinary DoT.

Blue/evolved Burn and Plasma are flags on their two existing classes, not missing
separate status classes. Plasma has a separate `addDamageTimer` for shock charge;
it stays native, while all its shock refresh/expiry/luck hits use Lightning's
control. Chim's `addDamageTimer` also remains a charge accumulator. The Zombie
visual's cosmetic Chim carrier never enters this damage path.

Other named damage-capable buff paths were checked: `CharacterDebuff_FanaticFlame`
actually derives from `CharacterBuff` and emits an unattributed self-cost, so it
is not PvP opponent damage. The old `CharacterBuff_LightningArmor` is a periodic
aura class without a demonstrated current playable consumer; the installed skill
uses `ActiveSkill_LightningArmor` and the ordinary chain-lightning utility. See
[spell/asset evidence](friendly-fire-spells.md#other-audited-searches).
Weapon, projectile, chain, retaliation, aura and ground-area attack rates otherwise
retain their own mechanics and use the shared PvP damage budget/hit-frequency
option; any debuffs they inflict use the controls above. There is no universal
Lightning/Fire/Ice elemental gate that would also slow direct attacks.

Current native metadata was read with REA; code paths were cross-checked against
the matching cached local decompilation and executable installed-game contracts.
The current `sharedassets0.assets` was also read with UnityPy: Plasma component
187142 sets both `damageId` and `electricDamageId` to `Debuff_Plasma`, overriding
the C# constructor defaults `Debuff_Burn`/`Debuff_Electric`. Its `Attack` writes
`DamageInstance.flag = DEBUFF_ELECTRIC`; its burn tick does not. Using that native
marker prevents serialized Plasma shock bypassing the Lightning control without
adding a second throttle to Plasma burn. Electric 187143, Chim 187141 and Wound
186523 retain their expected native IDs. StatusInstance 204764 is `SHOCK`, a
status-only modifier rather than a separate Lightning damage tick.
Assembly SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`;
MVID `ccf97024-28bd-48f1-9666-c768c2bd4b2b`.
REA member evidence:
`ev_9468670e58c43eedb119e8f4bbf6f2e5b27085d8a46b2ea8f48de617de4258f8`.
Game binaries and decompiled sources remain outside the repository. The native
contract suite enumerates concrete debuffs and will fail on an unclassified type
after an update. Different damage IDs/assets still require renewed investigation.

## Verification for 0.68.0

Release build passed with zero warnings/errors and deployment disabled. Passed
5,124 executable combat checks, 3,283 runtime integration checks plus 69 Bat
lifecycle checks, 1,707 interval policy/preset checks, 3,931 catalog
checks, panel policy tests and installed-game contracts, including all 11 debuff
types and 33 compiled Harmony API references against co-installed HarmonyX 2.9.
Tests cover independent/default/reset intervals, presets v1..34, live increases
and decreases, rejected attempts, Plasma variants, Freeze stun, Chim expiry,
Dark Cloud callbacks/FX, depleted/zero damage, ownership/connection transitions
and zero warmed allocations in periodic/pulse/strike paths.

The expanded UI uses a two-column scroll region with a visible scrollbar inside
the original 273x82 area; participant and master controls retain their space.
Independent read-only review found the revival edge case; it was corrected and
re-reviewed with no remaining material findings. The serialized Plasma regression
was also re-reviewed. Rendered scrolling and live host/stock-guest timing still
require in-game verification. No game launch or deployment was performed.

## Earlier verification

The 2026-10-19 default update has regression coverage for independent defaults,
per-effect/full resets, omitted rows in every supported preset version and
explicit previous Burn values surviving load/save. Cadence fixtures now check
that default Burn waits past 0.75 seconds and ticks at 0.9, while the other three
effects keep 0.75-second defaults. The two-second Burn-family lifetime cap is
unchanged. No deployment or live timing verification was performed.

Rechecked against `0.60.1` on 2026-10-16: all four independent UI inputs,
commands, 0.75-second defaults, Korean labels and saved-setting paths are already
present. No additional gameplay change was needed. Fresh runs passed 4,054 combat
checks, 2,266 runtime checks plus 69 Bat lifecycle checks, 343 interval policy/
preset checks, 3,571 catalog checks, installed-game contracts and the 33-reference
Harmony API audit. Existing explicit saved intervals remain unchanged; use each
effect's `reset` command to restore its default.

The local installed metadata and latest `Player.log` still report `0.57.0`, which
predates these controls (`0.59.0`). The updated addon must be installed and the
game fully restarted to expose them there. This recheck did not deploy or launch
the game, and does not verify rendered UI or live guest timing.

Original `0.59.0` verification:

Release build: zero warnings/errors with deployment disabled. Passed 3,877 combat
checks, 2,254 runtime checks plus 69 Bat lifecycle checks, 343 interval policy/
preset checks, 3,557 catalog checks, installed-game contracts and 32 compiled
Harmony references against co-installed HarmonyX 2.9. Independent read-only
review found no actionable issues.

Executable tests cover default/independent intervals, changed native timer
lengths, high BURNSPEED, live partial-progress edits, disable/unload, PvE/system/
ownership exclusions, mode edits, restart/rejoin and preset migration. All four
warmed timer paths allocate zero bytes across 1,000 fixture updates. Installed
contracts check timer fields, call boundaries, effect-kind mapping, unchanged
native charge/stun/expiry instructions and rejection of drifted IL. Assembly
SHA-256: `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.

Live host/stock-guest timing and rendered UI remain unverified. Check each effect
independently, edit during an existing effect, disable balancing to compare native
timing, and repeat after a run/reconnect. Nothing deployed; no game launch.
