# Independent PvP damage intervals

## Configurable duration cap (`0.85.0`, 2026-10-22)

**PvP Balance → Debuffs / Dark Cloud → Burn-family max duration (s)** controls
the existing shared lifetime cap. Default/reset is now **1.2 seconds**, replacing
the former hardcoded 2 seconds. Accepts **0.05..10 seconds**, up to two decimals.
The row has its own Apply and Reset buttons and EN/KO labels.

```text
/one friendlyfire dot-duration 1.2
/one friendlyfire dot-duration 2
/one friendlyfire dot-duration reset
/one save
```

Coverage remains **Burn, Plasma Burn and their blue/evolved forms**. These use
the same existing native Burn/Plasma lifetime handling, including Plasma's
normal expiry attack. Poison, Frostbite, Wound, Lightning, Freeze, Chim and
Dark Cloud gain no new duration limit. Their interval settings remain independent.
This cap applies only to permitted hostile player pairs with PvP balancing on,
in normal friendly fire and every shared match mode. PvE stays native.

A live value change uses elapsed time since the last application/refresh. Lower
limits retire overdue effects before their next server tick; larger limits let
still-active effects continue, but cannot revive an ended effect. Native
reapplications can refresh the capped lifetime, and native durations shorter
than the cap remain shorter. Turning balancing/PvP off leaves unexpired native
timers intact; no stat/timer restoration pass is needed. An interval equal to
or longer than the cap may produce no periodic tick unless the effect refreshes.

`FriendlyFireSettings` stores exact hundredths with a default-safe offset;
all commands, snapshots, modes and reconnects read that same live policy.
Preset **v46** saves nondefault `friendlyfire dot-duration N`, including while
balancing is off. Older/omitted rows inherit 1.2; invalid/duplicate/out-of-version
rows reject atomically. Duration edits do not reset scores, participants, modes,
damage allowance, pulse clocks or interval drafts.

The existing debuff update compares `Time.time` against application time plus
the configured duration, avoiding subtraction rounding at decimal boundaries.
No new polling, effect scans, stat writes or RPCs are introduced; native Destroy
and replication still retire effects for unmodified guests. Existing connection,
ownership, nested-callback and delayed-despawn guards remain in force.

Verification: non-deploying Release build passed with zero warnings/errors;
9,013 combat-hook checks, 13,973 runtime/session checks, 156 duration-policy
checks, 4,466 catalog checks and the panel/localization/installed-game suites
passed. Existing warmed Burn/Plasma update checks still allocate zero bytes.
Independent review found no actionable issues. Native live host/guest visuals
and gameplay were not exercised; nothing was deployed.

## Tick intervals

Expanded in `0.68.0`; schedule protection added in `0.69.0` and separate Plasma
shock in `0.71.0`, 2026-10-20.
Use **PvP Balance → Debuffs / Dark Cloud**
(`PvP 밸런스 → 디버프 / 먹구름`), with the scrollbar or mouse wheel to reach all ten
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
| Lightning / Shock / `lightning` | 0.5s | Electric's `Debuff_Electric` damage. `shock` is a command alias. |
| Plasma shock / `plasma-shock` | 0.9s | Plasma's `Debuff_Plasma` damage marked `DEBUFF_ELECTRIC`, separate from its burn ticks. |
| Freeze damage / `freeze` | 0.25s | `Debuff_Freeze` application/stack damage. Native stun is preserved. |
| Chim damage / `chim` | 0.25s | `Debuff_Chim` delayed expiry hit. No new periodic damage or change to its charge/lifetime. |
| Dark Cloud / `dark-cloud` | 0.7s | Native `ComboEffect_DarkCloud.FireLightning`, including ice variants and attached burn callbacks. |

```text
/one friendlyfire dot lightning 0.75
/one friendlyfire dot plasma-shock 0.9
/one friendlyfire dot dark-cloud 1
/one friendlyfire dot wound 1.5
/one friendlyfire dot lightning reset
/one friendlyfire status
/one save
```

The original four defaults are unchanged. `reset` restores only the named effect;
full friendly-fire reset restores all ten. Lower intervals admit more frequent
hits, but do not invent additional native attacks: pulse/Dark Cloud controls are
minimum gaps, and remain limited by native triggers and the shared damage budget.
The [configurable Burn-family lifetime cap](#configurable-duration-cap-0850-2026-10-22)
includes both Burn and Plasma and their variants. Without a refresh, an
interval at least as long as the configured cap may never tick before expiry.

## Preserve schedules (`0.69.0`)

Each debuff control has a **Keep schedule / 주기 유지** checkbox (eight originally,
nine since Plasma shock was separated in 0.71.0),
enabled by default. Dark Cloud is not a debuff reapplication path and always
retains its existing separate strike clock.

```text
/one friendlyfire dot burn preserve on
/one friendlyfire dot plasma-burn preserve off
/one friendlyfire dot lightning preserve reset
/one save
```

The first native immediate hit is allowed. Further hits of that effect from the
same attacker to the same opponent require the configured interval since the
last admitted hit. Reapplication, a replacement debuff instance, additional
stacks or another companion owned by that attacker cannot acquire a new clock.
Rejected repeats do not advance/postpone the clock or consume damage-budget
credit. Different effects, attackers and victims retain independent clocks;
all admitted damage still uses the common PvP budget.

Periodic effects such as Burn keep their normal first-tick delay; the option
does not introduce a new immediate hit. Stacks, duration refreshes, the configurable
Burn-family duration cap and Freeze's stun are unchanged. Suppressed damage is
not queued for later, and native shock charge/expiry handling still runs.

Turning an effect's checkbox off restores its native reapplication damage
behavior while keeping its configured periodic interval (where it has a periodic
timer) and shared PvP budget. For application/expiry-only Lightning, Plasma shock,
Freeze and Chim, off disables that effect's minimum-gap gate. Reenabling uses the latest
admitted hit, including hits while the checkbox was off, so it does not award an
extra immediate hit. Interval/toggle edits do not reset scores, modes or budgets.

Native investigation found that `CharacterDebuff.AddStack` and the base
`RefreshDurationTimer` refresh the **lifetime** timer, not the five periodic
damage timers. Their derived stack/refresh methods likewise leave periodic
progress intact. No speculative timer-restoration hooks were added. The new
protection extends the existing combat clock across all eight damage families.
It bounds repeated damage from reapplication/replacement; it does **not** remove
the damage increase from native stacking or establish that Piola Gavel's reported
strength was solely caused by timer resets.

Verification for this change: non-deploying Release build with zero warnings or
errors; 6,120 executable combat checks; 3,387 runtime integration checks plus 69
Bat lifecycle checks; 742 new schedule-policy checks, 1,708 interval-policy
checks, 3,945 catalog checks and panel tests. Installed-game contracts and all
33 compiled Harmony API references against co-installed HarmonyX 2.9 passed.
The independent review found an absolute-clock rounding case that could skip a
native periodic tick; an accumulated-frame regression reproduced it before the
precision fix and passes afterward. No live game or guest test was performed;
nothing was deployed.

The serialized prefab IDs were rechecked for schedule admission: Burn 184460
uses `Debuff_Burn`; Wound 186523 uses `Debuff_Wound`; Poison 186524 uses
`Debuff_Poison` with system damage off; Chim 187141 uses `Debuff_Chim`; Electric
187143 uses `Debuff_Electric`; Plasma 187142 uses `Debuff_Plasma` for both
components. Frostbite 186521 and Freeze 196504 use their hardcoded native IDs.
These are local asset observations, not assets shipped with the addon.

## Shared policy and persistence

### Separate Plasma shock and updated defaults (0.71.0)

Current defaults are Lightning 0.5s, Plasma shock 0.9s and Dark Cloud 0.7s. Wound
1.33s, Freeze damage 0.25s and Chim damage 0.25s are retained as requested.
Other defaults are unchanged. Explicit saved custom values remain intact.

`CharacterDebuff_Plasma.Attack` marks its shock hit with `DEBUFF_ELECTRIC` while
its periodic burn lacks that flag. The audited prefab (sharedassets0, 187142)
uses `Debuff_Plasma` for both IDs. Checking the ID/flag pair before generic electric
classification separates Plasma shock safely. The existing balance window owns
its clock; reconnect, life, floor and role resets clear it with all other clocks.
The settings, status, scrolling UI and EN/KO catalogs use the same registry.

Preset **v37** distinguishes the two shock interval/schedule controls. Older
v34..36 explicit Lightning intervals and v35..36 explicit schedule toggles are
copied to both effects on load, preserving their original shared meaning.
Omitted values inherit the new defaults. Modern Lightning-only overrides also
save as v37 so a reload cannot accidentally expand them to Plasma shock.

Dark Cloud still has its own per-attacker/opponent strike gate. It now reuses
the shared precision-safe `DebuffPulse` comparison: decimal intervals such as
0.7s must not spuriously reject the boundary because float subtraction is just
below 0.7. Admission remains reserved before native callbacks, including failed
callbacks; repeated same-frame strikes stay suppressed.

Verification: 2,013 interval-policy and 842 schedule-policy checks, separate
native electric/Plasma admission cases, 6,188 combat checks and installed-game
contracts. Live gameplay remains unverified. These changes were not deployed.

### Earlier schemas

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

Nondefault schedule toggles use **v35**, with
`friendlyfire dot-preserve-<effect> 0`. Omitted values and `preserve reset` enable
protection. v1..34 presets inherit enabled defaults and reject the new rows;
v35 also supports every earlier setting. Malformed, duplicated, noncanonical
and unsupported rows reject atomically. Neither `shock` nor `dark-cloud` is a
valid saved schedule key; the command alias `shock` saves as `lightning`.

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

All eight debuff families have independent **per-attacker/victim** damage
clocks in the existing `BalanceWindow`, checked when their schedule protection
is enabled. Lightning, Freeze and Chim have no periodic damage timer to wrap.
A successful admission reserves the clock
before native callbacks. Rejected repeats resolve to zero; they are not queued.
Since `0.77.0`, a budget-exhausted hit retains minimum damage and reserves its
interval even with zero funded charge; this prevents minimum-only pulse spam. Native
FX, stacks, expiry, charge and Freeze stun continue normally. Electric and Plasma
shock intentionally share the same Lightning clock, identified by the native shock
flag (or the original `Debuff_Electric` ID);
companion pulses use their owner's clock. Direct lightning/Bolt magic is not a
shock debuff and does not share this clock. Changing the interval compares against
the last admitted pulse; rejected attempts and sibling edits do not move it.
Clock comparisons allow only float timestamp precision to account for the
independently accumulated native timer and absolute clock. This is not a
frame-sized allowance; the same timestamp can never admit another hit. Tests
accumulate frame deltas at 60/120/144 FPS across different game uptimes to prevent
false early-hit rejection followed by an extra full interval of waiting.

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
