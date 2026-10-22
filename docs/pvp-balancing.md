# Optional PvP balancing

## Optional Thorns suppression (`0.89.0`, 2026-10-22)

**PvP Balance → Artifacts / Debuffs → Suppress Thorns** (Korean: **가시 반격 차단**)
defaults off. `/one friendlyfire suppress-thorns on|off|reset` edits the same
host policy; `reset` restores off. It requires PvP balancing and a currently
hostile player pair. Normal friendly fire, Deathmatch, Tournament, Play Tag and
Zombie Survival share this damage path and participant/team rules.

Only native `Ability_Thorns` with `EDamageFromType.None` targeting a player is
suppressed. `UnitAvatar.ApplyDamage` constructs that return from the damaged
avatar and immediately applies it to the incoming damage source. The existing
installed-game reflection contract verifies the ID, origin, metadata and target.
Rejecting at the shared hit prefix avoids native damage callbacks, guard MP
costs, budget reservations and kill attribution. Shared late permission checks
also observe a setting change inside a native callback. Weapon reflection,
Venom Spore Pouch, enemy-origin Thorns and outgoing Thorns against NPCs keep
their existing behavior. Player-owned companions cannot bypass the restriction
when retaliating against a hostile player.

No native Thorns stats are rewritten. Disabling balancing/the option restores
normal retaliation immediately. Live edits retain matches, K/D/A, selection and
damage credit. Event-time policy lookup covers replacement connections; no
per-player suppression cache, polling or allocation is introduced. `/one save`
stores the enabled preference in v48 presets; older files default off.

Regression coverage includes nested/independent returns, sibling reflections,
PvE in both directions, guard costs, live edits inside callbacks, match overlays,
reconnect/run/preset lifetime and off/reset recovery. The existing native damage
replication serves unmodified guests; live multiplayer/UI verification remains
an in-game check rather than a fixture guarantee.

Verification: Release build (zero warnings/errors), 9,241 executable combat
checks, 14,084 runtime checks, 125 Thorns policy checks, localization/catalog
checks and installed-game compatibility contracts passed. The warmed suppressed
return path allocated 0 bytes across 1,000 hits. No deployment or game launch.

## Shared recoverable burst (`0.88.0`, 2026-10-22)

The [slow-burst fix](pvp-burst-audit.md) removes the old unconditional per-hit
curve. Heavy hits and rapid packets share one reserve, now defaulting to 75%
of victim maximum HP. Continuous refill remains 70%/s; saved values are retained.
The minimum positive hit, native defenses, optional cadence controls, role
modifiers and shared lifecycle cleanup stay intact. No new per-hit allocations
or per-frame work; PvE remains native.

## Autonomous artifact hit limit (`0.86.0`, 2026-10-22)

[Optional per-source cadence](pvp-autonomous-artifacts.md): off / 2s by default,
configurable in PvP Balance or `/one friendlyfire artifact-rate`. Uses shared
connection/life/session state; no native stat writes or per-frame work. Valid
hits retain the existing damage-budget minimum, while this explicitly enabled
hard-rate control rejects extra artifact impacts inside its interval.

Added in `0.52.0`, 2026-10-13. Host-only installation; the existing native damage
and FX replication continues to serve stock guests. Default: **off**.

Since `0.82.0`, the dedicated **PvP Balance** tab contains the general balancing
controls and debuff/Dark Cloud intervals. Combat retains friendly fire, supplies
and participants. See the [current panel guide](control-panel.md).

Since `0.83.0`, [Rapid Hits](pvp-hit-frequency.md#controls) exposes the rolling
detection window, full-damage rate, reduction exponent and stored burst capacity,
alongside continuous refill. Current defaults are 1s, 4 hits/s, 0.5, 75% and 70%/s.
There is no periodic budget reset. Live parameter edits preserve match/KDA state
and existing credit; larger capacities do not create an immediate burst.

## Additional defensive effects (`0.81.0`, 2026-10-21)

[Defensive audit](pvp-defensive-effects.md): Elastic Band refunds dash charges
at most once every 2s per defender against players. Extended weapon-counter
immunity is limited to 0.35s per activation plus native latency allowance.
Existing balancing toggle; no new preset fields or PvE stat/timer changes.
Finite dodge barriers and consumed lethal saves remain native.

## All Damage Amplified limit (`0.80.0`, 2026-10-21)

[Optional amplification curve](pvp-amplification.md): soft 50% / hard 100%
bonus defaults, initially off. PvP Balance and
`/one friendlyfire amplification` control the native combined attacker/leader
contribution before defenses and the existing PvP budget. Character stats,
native penalties and PvE stay unchanged; v43 presets save nondefault settings.

## Responsive damage allowance (`0.77.0`, 2026-10-21)

[Hit-frequency scaling](pvp-hit-frequency.md) now separates direct attack cadence
from incidental procs, debuffs and companions. Both histories retain one shared
damage allowance and the same connection/life/match cleanup.

Valid positive hits keep at least one pre-shield damage when allowance runs out;
frequency scaling and budget exhaustion no longer discard them. Only funded
credit can be refunded. This deliberately permits minimum-only barrages above
the configured refill rate, so the budget is no longer a strict DPS/TTK bound.
Native defenses, zero/invalid hits, explicit zero damage settings, effect-specific
tick schedules and Dark Cloud's strike interval remain unchanged. No extra
per-frame work, network state or per-hit allocation is introduced.

## Debuff schedule protection (`0.69.0`, 2026-10-20)

[Keep schedule](pvp-dot-intervals.md#preserve-schedules-0690) adds eight independent,
default-on toggles beside the interval controls. Reapplication cannot bypass the
same attacker/opponent/effect clock. First native immediate hits, duration
refreshes, stacks and Freeze stun remain. Commands use
`/one friendlyfire dot <effect> preserve on|off|reset`; nondefault toggles save in
v35 presets. Dark Cloud retains its separate strike limiter. Only balanced PvP
uses these clocks; no temporary stats or client-side addon state are introduced.

## Expanded interval controls (`0.68.0`, 2026-10-20)

[All audited debuff damage families and Dark Cloud](pvp-dot-intervals.md) now have
independent live controls: Burn, Plasma Burn, Frostbite, Poison, Wound,
Lightning/Shock, Freeze damage, Chim damage and Dark Cloud. Lightning includes
Plasma's separate shock component. Their new overrides save in v34; existing
values and all four original defaults are preserved. Freeze stun, ordinary
spell/weapon rates and PvE behavior stay native. See the linked audit for exact
timing boundaries, defaults and excluded non-damaging/scripted effects.

## Minimum positive damage (`0.67.1`, 2026-10-20)

All admitted friendly-fire hits now have a one-point minimum at the existing
post-defense, post-PvP-scale, pre-shield boundary. For example, 10 resolved damage
at `0.05%` becomes 1 instead of 0.005. This applies with balancing on or off in
casual combat, Deathmatch, Tournament, Play Tag and Zombie Survival. Fractional
results above one remain unchanged; this is a minimum, not integer rounding of
every hit. Normal PvE does not enter this path.

The minimum requires finite, positive resolved damage and a positive permitted
result after balancing. Zero percent, zero incoming-role scale, zero/negative/
nonfinite damage, native protection, unselected/non-opposing targets, rejected
effect pulses stay harmless. Since `0.77.0`, budget exhaustion alone no longer
rejects a valid positive hit. Ordinary shields absorb the
minimum normally; MP shields can now charge one MP for an otherwise sub-one hit.
The minimum is not applied again after absorption, armor or another native veto.
Consequently it guarantees positive pre-shield damage, not one HP lost through
every native defense or a shield that has already absorbed part of the hit.

The budget uses pre-incoming-role units, so its minimum is `1 / incomingScale`.
Since `0.77.0`, it reserves only available funded credit before callbacks, with
no debt for any remaining minimum damage. Native undelivered-damage refunds
cannot exceed that funded reservation. Double/float arithmetic residue is
excluded from refunds and depletion so it cannot manufacture allowance.
Existing pair receipt, life, floor and epoch cleanup owns the shared pool.
Earlier `0.67.1` behavior charged the whole minimum and rejected hits at
exhaustion; that policy has been replaced.

No new hooks, RPCs, settings, polling, state collections or guest addon are
needed. The executable Harmony fixture reproduced the original low-scale loss
before the change. Regression checks cover HP/MP/ordinary shields, zero and
invalid inputs, guards, invulnerability, reflection, spell/companion/proc paths,
role reductions, tiny HP budgets, nested reservations, debt/refill and frequency
scaling. These are fixtures and installed-code contracts, not live multiplayer
verification; no game launch or deployment was performed.

Historical `0.67.1` verification: Release build passed with zero warnings/errors; 5,063 executable
combat checks, 3,205 shared runtime checks (plus 69 Bat lifecycle checks), and the
portable/installed-game contract suite passed. Review added high-HP regressions
at 1,710 and 2,300 HP: accumulated conversion residue previously bought an extra
minimum hit after exact burst depletion. Capacity-relative dust suppression now
keeps those bursts at exactly 342 and 460 damage respectively.

Updated in `0.60.4`: [stat-based Evasion](#evasion-cap-0604-2026-10-17) is capped
at 50% before attacker anti-Evasion reductions. Lower chances and guaranteed
item dodges remain native; no extra setting or stat override is introduced.

Updated in `0.60.0`: [optional hit-frequency scaling](pvp-hit-frequency.md) adds
a separate default-off checkbox/command. Above roughly four hits per second per
opponent, it progressively reduces per-hit damage before the existing budget.
Both mechanisms share pair lifetimes; frequency toggles do not refill tokens.

Updated in `0.59.0`: [independent DoT intervals](pvp-dot-intervals.md) replace the
earlier cadence rule for Burn, Plasma Burn, Frostbite and Poison. Each defaults
to 0.75 seconds and has separate live controls, commands and saved settings.

Updated in `0.59.2`: [Burn's two-second lifetime cap](#burn-duration-cap-0592-2026-10-16)
caps each balanced PvP Burn application or refresh, regardless of duration bonuses.
Expanded in `0.60.1` to Plasma Burn, including blue/evolved forms of both families.

Updated in `0.53.0`, 2026-10-14: balancing also limits rapid damage with a shared
budget per attacker/opponent. Existing saved balancing settings enable both
layers; there is no new setting, schema migration or guest installation.

Updated in `0.54.2`, 2026-10-15: shared refill increases from 30% to **40%**
max HP/s, and eligible periodic PvP debuff ticks run at **75% of native frequency**.
Shock/Freeze damage pulses are limited to **once per 0.5 seconds each** per
attacker/victim pair; Freeze still stuns normally. The 20% burst capacity,
per-hit curve, Dark Cloud gate and lifecycle resets remain.

Updated in `0.54.4`, 2026-10-15: Shock and Freeze each allow a damage pulse
every **0.25 seconds** per attacker/victim pair, replacing the 0.5-second limit.
This reuses the same clocks and lifecycle handling in casual friendly fire,
Deathmatch and Tournament. Freeze's native stun is preserved. This is a damage
admission limit, not an automatic tick generator: native procs and the shared
damage budget still determine whether a hit deals damage. Burn's cadence remains
native-speed-dependent with the existing 0.75 elapsed multiplier; it is not a
fixed 0.25-second timer. The shared 40% HP/s refill is unchanged by this tuning.

Updated in `0.55.0`, 2026-10-15: refill is now configurable, default **100% of
victim maximum HP per second**. The Combat panel's **PvP refill (%/s)** input
(`PvP 회복률 (%/초)`) and **Apply refill** button use the same command as chat.
The panel retains its dimensions; K/D/A reset and Revive All remain at the top.

Updated in `0.55.1`, 2026-10-15: default refill is **70% of victim maximum HP
per second**. Explicit saved refill values remain unchanged. Presets without a
refill row, including older saves of the former default, inherit 70. Both refill
reset and full combat reset restore 70. The 20% burst and all other limits remain.

Updated in `0.57.0`, 2026-10-16: [Play Tag chaser advantages](play-tag.md#chaser-advantages-0570-2026-10-16)
compose at the same resolved player-hit boundary. The active chaser can multiply
the global refill (default x2, enabled); it requires this balancing toggle.
A separate enabled-by-default 75% incoming multiplier works with or without
balancing. These options never write native stats, change PvE or enlarge burst
capacity. Incoming reduction is accounted for before unused budget is refunded,
so rapid attacks cannot bypass it. Role/configuration changes retain credit and
conservatively limit elapsed refill until the next stable interval. Outside an
active living/armed Tag carrier, both modifiers are inactive.

Enable **PvP Balance → PvP balancing** (`PvP 밸런스 → PvP 밸런스 보정`) or use:

```text
/one friendlyfire balance on
/one friendlyfire balance off
/one friendlyfire refill 70
/one friendlyfire refill 125.5
/one friendlyfire refill reset
/one friendlyfire status
/one save
```

Refill accepts **1..300**, up to two decimal places, with an optional `%` suffix.
It is the damage allowance replenished per second, not HP regeneration or the
ordinary damage percentage. It applies only while PvP balancing is enabled.
`refill reset` restores 70 without changing other preferences or interrupting
an active match. Full `/one friendlyfire reset` restores all combat defaults.
Use `/one save` to persist the choice. Nondefault refill requires preset v24;
older presets inherit 70, and default refill adds no row or schema requirement.

The preference is independent of the friendly-fire master switch. It affects
current permitted player pairs in casual friendly fire, Deathmatch, Tournament and Play Tag
only while effective friendly fire is active. Empty selection means everyone;
nonempty selection and Tournament's current pair keep their existing rules.
Changing balancing does not stop a match or clear K/D/A. Touching the master
toggle still uses the existing stop/recovery semantics.

## Evasion cap (`0.60.4`, 2026-10-17)

User-approved limit: while balancing is enabled for an eligible PvP hit, cap
the victim's **stat-derived dodge chance at 50%**. A native 20% chance remains
20%; a native chance of about 77% becomes 50%. Attacker anti-Evasion then reduces
that capped chance normally: 50% anti-Evasion reduces 50% to 25%. This is a cap
on the calculated probability, not a cap of 50 Evasion stat points.

The existing **PvP balancing** checkbox or `/one friendlyfire balance on|off`
controls this in casual friendly fire, Deathmatch, Tournament and Play Tag.
Selection, current match pair/countdown rules, live connection receipts and
player-owned companion attackers use the existing combat policy. Enemy attacks
against players, attacks against enemies/NPCs/companions, self/system damage and
guest-side authority stay native. Disabling balancing or ending the PvP session
removes the adjustment immediately; no stats need restoring. Native costume,
equipment and buff changes are read again on the next hit.

Native `UnitAvatar.ApplyDamage` computes:

```text
chance = 100 * Log(min(Evasion, 10000) / 6200 + 1) * 0.8
```

`FriendlyFireEvasionHooks` inserts one scalar adapter immediately after this
formula, before the native lower clamp, `IGNOREEVASION` and dodge roll. The
installed formula already limits stat-based chance to roughly 77%, but that
still avoids over three of every four eligible attacks at maximum investment.
The new limit retains up to a one-in-two chance and preserves the native RNG
calls and branch logic. A failed stat roll can reach the existing conditional
item-dodge roll more often; the number of draws in such a hit can therefore differ.

The separate `DamageInstance.absoluteEvasionPercent` and `ABSOLUTEEVASION`
branches remain native. In the inspected source, `Charm_ShadowEye` grants a
one-hit guaranteed dodge; `Charm_BrokenSapphire` consumes its barrier through
`USE_ABSOLUTEEVASION_STAT` and `OnEvade`. Their charges, callbacks and guaranteed
success are preserved, so the cap is not a blanket limit on all ways to avoid
damage. Native guard/parry/dash protection, Evasion-disable and MP-shield checks
also remain. Elemental-effect damage and falls still bypass Evasion normally.

The **C menu continues showing native Evasion and native dodge probability**.
The calculation adjustment never edits stats or damage objects, so saved
presets, PvE, native stat UI and reconnects cannot inherit a temporary override.
English/Korean command help describes the cap. No new preset schema, packet,
guest script, asset, timer, scene scan or per-player cache is needed.

The adapter composes with the existing damage transpiler and compatibility
guard. Missing/duplicated/changed formula anchors reject installation rather
than rewriting an unrelated stat/RNG path. Installed contracts verify every
original instruction, branch/exception entry, item-dodge path and RNG call is
preserved, plus the combined combat hook. The barrier-callback contract scopes
its lookup after the consumption flag, distinct from the earlier dash callback.

Verification: Release build with zero warnings/errors; 4,207 executable combat
checks; portable/installed-game contracts and 33-reference Harmony 2.9 API audit
passed. Regression first reproduced a max-stat player evading a 60% roll before
the fix. Coverage includes the 50% boundary, lower/extreme stats, anti-Evasion
ordering, item consumption, native exclusions, toggle/selection/connection
changes, nested PvE and exception cleanup. Warmed balanced Evasion records zero
allocated bytes over 1,000 fixture hits; this is not a Unity FPS measurement.

Installed game assembly SHA-256:
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Live testing remains: compare high/low Evasion with an unmodified guest attacking
and defending, toggle balancing during all shared match modes, test a guaranteed
dodge item and attacker anti-Evasion, and compare with monsters in the same run.
No build was deployed and the game was not launched.

## Burn duration cap (`0.59.2`, 2026-10-16)

Current behavior since `0.85.0`: this shared Burn/Plasma/variant cap is
[configurable](pvp-dot-intervals.md#configurable-duration-cap-0850-2026-10-22),
with a 1.2-second default. The two-second values below describe its original
implementation; coverage, refresh, ownership and native-expiry handling remain.


Scope expanded in `0.60.1` (2026-10-16): the original cap covered only
`CharacterDebuff_Burn`. Plasma is a sibling class and previously escaped it.

While balancing and positive-damage friendly fire are enabled, **Burn and Plasma
Burn, including Blue Flame variants, expire within two seconds of their latest
application or native refresh**, at the first server update at/after the deadline.
General debuff-duration bonuses
and `BURNDURATION` cannot increase this window. A new application to an active
Burn may refresh it for up to two more seconds, including at maximum stacks.
Non-renewing stacks keep their native behavior and do not reset the deadline.
Repeated successful applications can therefore sustain Burn; after the last
application, its remaining lifetime is at most two seconds. Native durations
shorter than two seconds remain shorter. The independent Burn/Plasma tick
intervals and shared damage budget are unchanged. Without refreshes, either
effect configured with a tick interval of two seconds or longer can expire before
its first periodic tick. Plasma still runs its native charged shock on expiry;
that hit uses the existing PvP damage rules, pulse gate and budget.

The cap uses the current permitted player pair in casual friendly fire,
Deathmatch, Tournament and Play Tag. Player-owned companion Burn uses the same
owner receipts. PvE in either direction, self effects, non-player victims,
unselected players and disallowed Tournament pairs retain native expiry.
Shared damage rules still block nonparticipating targets. Unrelated debuffs such
as Poison, Frostbite and Shock retain their native lifetimes.

Turning balancing on applies the cap to already-existing tracked Burn using its
latest application/refresh age, including refreshes made while balancing was
disabled. An effect unrefreshed for two seconds ends before another tick.
Turning balancing/FF off removes the cap from effects that have not ended.
An effect already retired by the cap/guard cannot resume damage during native
delayed despawn.
No new command, toggle, preset field or translation is needed: this is part of
the existing PvP balancing option.

### Native behavior and implementation

`CharacterDebuff.InitializeAndSpawn` calculates its timer length from base plus
additional duration and the caster's percentage bonuses. Burn's override adds
both general `DebuffDuration` and `BURNDURATION`. `AddStack` calls
`RefreshDurationTimer`, whose base implementation resets elapsed time to zero.
Burn inherits this method. Plasma's override performs its native shock attack,
then calls the base refresh and resets its separate shock accumulator. The same
base postfix therefore sees both families without overriding either refresh.
The refresh happens before the maximum-stack check, so applications at maximum
stacks can still renew the duration.

`IsBurnFamily` explicitly recognizes `CharacterDebuff_Burn` and
`CharacterDebuff_Plasma`, including subclasses. In the inspected assembly,
`BLUEBURNCHANGE`/`NetworkisBlueBurn` and `BURNEVO` select Blue Flame and evolved
behavior within these types. Plasma's native `CompareID` also matches `BURN` and
`ELECTRIC`, but a generic elemental/string-ID match would incorrectly include
unrelated Shock effects. There are no additional native subclasses of these two
types. `CharacterDebuff_FanaticFlame` is instead a `CharacterBuff` used by the
Tentacle monster, with ownerless damage; it is not a player-attributed Burn
variant and remains outside PvP balancing. Future unrelated effect classes need
their native lifecycle audited before adding them to this family.

`FriendlyFireBurnDuration` reuses `FriendlyFireDebuffLifetime`'s existing weak
origin record, with the latest application time and an ended flag. A postfix on
the native refresh updates that timestamp only for valid, live Burn-family receipts.
It does not reset periodic tick progress. The validated pre-update path
checks the two-second deadline before native damage and calls native `Destroy`
once. Native cleanup retains Plasma's `DestroyInner` shock, removes status,
calls removal callbacks and schedules the normal stock-client despawn.
No prefab, native timer configuration, stat or
HUD-duration field is changed; remaining native timing needs no restoration.

Native `CharacterDebuff.Update` invokes `OnUpdate_Server` **before** its own
expiry check, including during the 0.5-second delayed despawn. The ended flag
blocks subsequent updates even if balancing changes after the cap fires, avoiding
extra ticks during that delay. New initialization replaces the record; refreshes
only update its timestamp and cannot revive a retired effect. Connection
retirement/owner transfer are handled first by existing
origin validation, so a reconnect cannot inherit the previous effect.

Unity's scaled `Time.time` measures time since application/refresh; pausing does not advance it.
The existing weak table is cleared on unload, and native despawn owns effect
cleanup. No extra dictionary, polling loop, per-frame allocation, packet or guest
addon is introduced. A skipped/slow frame expires before the next damage tick
rather than producing overdue ticks. The current game assembly fingerprint is
unchanged from [domain findings](domain-findings.md).

### Verification

Executable coverage reproduces the former long-duration Burn and Plasma, then checks the
exact two-second boundary, large bonuses, direct refresh, stacks, short native
expiry, delayed-despawn damage, new applications/reinitialization, live toggles,
PvE/owner/selection/match exclusions, companion death, reconnection, pause,
exceptions and unload. Warmed duration checks allocate zero bytes in fixtures.
Plasma coverage also exercises its override-to-base refresh, one expiry attack,
stale-owner expiry rejection, derived classes and unrelated elemental exclusions.
Installed contracts verify both native blue/evolved identities, Plasma's
refresh/expiry damage dispatch, native duration/refresh ordering,
cleanup/network despawn and shared pre-update integration.
`0.60.1` Release build passed with zero warnings/errors; 4,054 combat checks, 2,266 runtime
checks plus 69 Bat lifecycle checks, installed-game contracts and the 33-reference
Harmony 2.9 API audit passed. Independent review found no actionable issues.
Actual Unity/stock-guest timing and visuals still need live testing; nothing
was deployed or launched.

## Guard MP cost (`0.58.0`, 2026-10-16)

**Historical behavior below.** `0.80.1` replaces this guard-only implementation
with a strict default **5% minimum after reductions**, including parries and
automatic shields. See [current shield MP rules and audit](pvp-shield-mp.md).

Default raised from 5% to 8% in `0.78.1` (2026-10-21).

While balancing is enabled, blocking an admitted PvP hit costs **8% of the
defender's current maximum MP**, before native reductions. Guard Efficiency,
perfect guard's half-cost and native item/cost callbacks still apply. At 1,000
maximum MP, an ordinary block costs 80 MP, a perfect guard 40 MP; 40% Guard
Efficiency lowers those to 48 and 24 MP. This is charged on a successful block,
not simply on pressing or holding right-click.

Use **Combat → PvP balancing → Guard MP (%)** (`방어 MP (%)`) or:

```text
/one friendlyfire guard-mp 8
/one friendlyfire guard-mp 7.5
/one friendlyfire guard-mp reset
/one save
```

Range: 0..100%, up to two decimals, optional `%` suffix. Zero explicitly waives
the balanced guard cost. Positive fractional MP costs round up; a genuinely
zero native cost remains zero. Reset restores 8% without touching other
preferences, scores, participants or the current match. Full friendly-fire reset
also restores 8%. Saving a nondefault value uses preset v27; all older supported
presets with no guard override inherit 8%. Explicitly saved custom values,
including 5% or 10%, remain unchanged. Default values add no extra preset row/version
requirement.

The Combat panel has compact **Combat** and **PvP balancing** subsections. The
balancing toggle, refill and guard editors are together; K/D/A reset, Revive All,
the master toggle and participant selection remain accessible in both sections.
Each numeric editor preserves its own draft until Apply or a page/scope change.

Implementation uses the existing authoritative `ApplyDamage` hit scope. Only the
final float-to-int conversion before the guard branch's `UseMp` is replaced:
`ceil(nativeModifiedCost / 10 * effectiveMaxMp * configuredPercent / 100)`. It reads both
current policy and maximum MP after native callbacks. No stats are rewritten,
no guard cost is cached, and no restore pass, polling or network message is added.
Native `UseMp` retains MP callbacks, SyncVar replication for unmodified guests,
infinite-MP handling and the subsequent guard break. Separate parry and MP-shield
paths remain native. There is no scaled spending for PvE, self/system damage,
non-player victims, excluded participants or Tournament bystanders. Owned
companion attacks against permitted players use the same guard rule.

### Large-MP guard correction (`0.78.2`)

The installed `UnitAvatar.MaxMp` getter evaluates
`maxMp + (int)((float)(GetCustomStat(FinalMP) * maxMp) / 100f)`.
The product and final addition are signed 32-bit operations. Overflow can wrap
the result into a smaller positive maximum or a negative maximum. Previously,
`GuardMpCost` read that getter and clamped negative results to zero, allowing a
free balanced PvP guard despite a positive cost and large positive inputs.

`GuardMaximumMp` now reads the same current base MP and Final MP inputs, widens
their product to `long`, and bounds the computed total to `0..int.MaxValue`
before percentage scaling. Ordinary representable products keep native float
rounding; overflowing products use double division. The guard's native
reductions and final positive-cost ceiling are unchanged. It reads values after
guard callbacks and performs no stat writes, reflection, allocations, caching
or per-frame work. This fixes the guard calculation only; it does not globally
rewrite the game's maximum-MP getter or unrelated native UI/recovery behavior.

Example reproduced in the executable hook fixture: base MP 10,000,000 plus
297% Final MP should yield 39,700,000 maximum MP. Native multiplication wraps
negative, so the old hook charged zero; the corrected 8% ordinary block costs
3,176,000 MP. Product wrap to positive values and addition overflow are also
covered. Values beyond the game's signed-integer MP capacity saturate rather
than wrap.

Star Aquamarine is item `1124`, name key `Item_FinalMP_Name`, resource prefab
`sharedassets0.assets:67923`. Its `Charm_StatusInstance` component
(`205884`) grants `FINAL_MP` values `[10,20,33,45,60]` by level. Its Lake category
uses `ComboEffectBase` at prefab `84901` / component `184227`: thresholds 3/6/9
grant maximum-MP/regen or MP-skill-damage bonuses, not `INFINITYMP`.
Nine Aquamarines alone at ordinary base MP do not demonstrate this overflow;
the actual reported maximum MP and right-click versus passive-shield path
remain unconfirmed.

Other distinct semantics remain explicit: native Guard Efficiency/perfect guard
can reduce the configured percentage; it is not a hard floor after reductions.
Native Magic Reflow (`INFINITYMP`) uses its fixed maximum and waives `UseMp`;
the artifact stack above does not itself enable it. Passive `MPSHIELD` absorption
has a separate damage-to-MP cost and is not a right-click guard. Those paths were
not broadened by this arithmetic fix.

Evidence: installed assembly SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`;
fresh ILSpy 11.1 exports of `UnitAvatar`, installed EN/KO localization, and
UnityPy asset reads (the generic ComboEffect tree failed decoding, so its raw
serialized threshold/status strings were checked against `ComboEffectBase`).
REA `ev_026b08ae3701bcbf4fdb06d0b9bc2a96a1799eb550783ea54de4c740e546b039`
established artifact identity but reported unavailable member analysis due to its
64-MiB metadata budget; it is not evidence of decoded method behavior.
Decompiled source/assets remain outside the repository. These are static and
fixture observations, not a live multiplayer reproduction.

Verification: the new overflow reproduction failed before the correction and
passed afterward. Release build passed with zero warnings/errors; 6,240
friendly-fire executable checks, 13,753 runtime checks plus 69 Bat checks, and
the portable/installed-game compatibility suite passed. The latter verifies
the current getter inputs/order and unchanged guard spending/break boundaries.
Warmed guard scaling still allocates zero bytes per 1,000 blocks. Independent
code review found no actionable defect. Nothing was deployed.

Disabling balancing, ending effective friendly fire or unloading the addon
immediately restores the native cost. Deathmatch, Tournament and Play Tag all
read the same setting live; guard edits do not stop/reset matches or refill damage
budgets. Play Tag's chaser damage/refill bonuses do not change guard spending.

Numerics: native float subtraction can yield `0.10000038` for 99% Guard
Efficiency. Exact float representations of the native 0.05 cost grid, or exact
results of its normal/perfect-guard formula, are normalized to integer twentieths
before scaling. This avoids an extra MP at exact boundaries without a broad
epsilon that absorbs nearby custom callback values. Identical float values from
custom callbacks are inherently indistinguishable; other fractional values and
positive near-zero values are retained. Invalid balanced amounts become zero and
overflowing positive costs saturate at int max. Outside balanced PvP the original
native conversion is preserved.

Installed-game contracts validate the 10f base, GuardResist read, perfect-guard
0.5f, durability callbacks, exact guard spend and native MP/break tail against
assembly SHA-256 `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Changed contracts reject installation rather than patching a later MP-shield
spend. Executable regressions cover all 0..100 Guard Efficiency levels, perfect
guards, live settings/MP changes, nested hits, callback exceptions, scope loss,
rejoins, all match modes, numerical extremes and unload. The warmed guard path
allocates zero bytes across 1,000 blocks in the fixture.

Live host/stock-guest validation remains required: compare ordinary/perfect
guards and Guard Efficiency at different MP pools, empty the MP bar to trigger
guard break, edit the setting mid-match and disable balancing during PvE/PvP.
Confirm visible MP and guard break on guests, and the compact EN/KO layout.
Nothing was deployed and the game was not launched for this change.

Verification for `0.58.0`: non-deploying Release build succeeded with zero
warnings/errors; 3,851 combat checks, 2,238 runtime checks and 69 Bat lifecycle
checks passed. Portable/settings/native contracts passed, including 148 guard
settings checks, 3,519 catalog checks and 32 compiled Harmony references against
co-installed HarmonyX 2.9. Independent review caught the float ceiling issue;
regressions now cover native cancellation and distinct nearby callback amounts.

## Historical single-hit model and trade-offs

The following records the original per-hit curve, removed in `0.88.0`. Current
combat uses the shared recoverable burst described at the top of this document.

The initial `0.52.0` design used per-hit softening only. After observing rapid
attacks such as Piola Gavel and Pallas' Cards, the user requested the earlier
damage-budget approach instead of live stat scaling. Native stat rewriting would
need to track equipment, costumes,
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
are not normalized. Debuff damage cadence is the targeted exception described below.
The budget below limits sustained damage from one attacker;
focus fire, knockback and crowd control can still be strong. Strong
healing or defensive builds may remain hard to kill. This reduces extreme hit
power, not every possible build advantage. Since `0.67.1`, positive admitted
sub-one hits use the minimum described above. The setting never changes the
host damage slider automatically.

## Rapid-hit budget (`0.53.0`, configurable in `0.55.0`)

Historical introduction below: since `0.88.0`, default capacity is 75% and there
is no separate per-hit curve. Continuous refill and the existing ledger remain;
current controls and minimum-one behavior are described above.

Each directed attacker/player-victim pair has a token budget with **20% of actual
victim maximum HP capacity** and **70% of maximum HP per game second refill by default**.
Refill is continuous, pauses with the game clock and stops at capacity. All of an
attacker's weapons, artifact projectiles/procs, debuffs and companions share the
same pool against that victim. Changing damage IDs, weapons or projectile objects
does not grant another pool. Other attackers and other victims remain independent;
reflection uses the reverse directed pair. Dark Cloud retains its separate limit.

For fixed maximum HP `H`, funded damage credit over `T` seconds is bounded by
`H * (0.20 + R*T)` from a full pool (`R = refill percent / 100`, before role
modifiers). Since `0.77.0`, minimum-only damage can exceed that allowance, so it
does not establish a minimum time to kill. Actual cadence, defenses, healing and
shields affect the outcome; multiple attackers remain independent. The budget gives
rapid direct/magic hits more room without changing native impact detection or
invulnerability. It also applies to the same shared artifact/companion/DoT pool;
there are no separate additive allowances that multi-effect builds can combine.

Changing refill preserves current tokens, combat scores, match state, pulse clocks
and the combat balance revision; it never grants another initial burst. On the
first hit observing a rate edit, use the lower of the prior and current refill
rates for elapsed time since the previous hit, then retain the new rate. This
conservatively avoids retroactive faster credit and makes reductions immediate.
Rate changes are O(1), with one extra double per existing pair budget, no new
polling/scene scans and no new network messages. Native stats and PvE are untouched.

Reserve the already-softened hit before shields/callbacks to prevent recursive
procs consuming the same tokens twice. On successful completion, the native
`AddReceivedDamage` reports for shield, MP shield and HP determine the actual
spending; refund any unused reservation. This matters for native MP shields,
which truncate fractional damage to integer MP: an unused fraction must remain
available for later hits instead of being lost every frame. Exceptional native
hits retain reservations conservatively because some writes may already have
occurred. A reservation's generation prevents its refund entering a replacement
pool after nested toggles or other resets.

The budget is therefore **actual HP/shield damage**, not pre-super-armor pressure.
Super armor still reduces individual hits normally. When incoming attacks fully
saturate the allowance, their refunded reductions can be spent by following hits,
so super armor does not lower the final saturated HP-per-second ceiling. Overkill
clamped by native current HP is refunded too. Native additional-life recovery
never enters the dead-to-`Revive` lifecycle; it continues the existing budget,
native invulnerability and gradual refill rather than granting a new burst.

Tokens are HP fractions, not cached HP points. Max-HP changes affect subsequent
damage/refill without resetting available capacity; current low HP never shrinks
the budget. No damage debt is delayed and emitted later. Empty-budget positive
hits retain the pre-shield minimum, while native callbacks still run. This is not a
universal rate limit on stuns, knockback, debuff application, visuals or direct
health writes outside the audited damage pipeline.

Native `Charm_PallasCard.OnBeginAttackAnimation` emits three homing bullets per
proc via `Bullet.Pool.Spawn`; their player impacts share the owner's pool. The
general damage boundary covers rapid attacks without depending on an item-name
allowlist. It does not change the native card timer, luck roll or bullet count.
Piola Gavel's exact asset identity was not needed or separately established here;
its combat must still be verified in a live match along with other rapid effects.

Receipt-keyed weak tables reuse current connection identities. Combat/participant
epochs, balance revisions, source/victim floors and dead-to-revived life counters
invalidate old pools. Master off/on, balance off/on, KDA/selection changes, match
transitions, run/session replacement and same-object reconnects start fresh.
Death alone does not refill a surviving queued attack's pool. Immediate successful
revival starts a fresh life; the 3-second automatic respawn is covered as well.
Native revival preparation invalidates only that player's lives, and failed
revival cannot permit attacks while the player remains dead. Runtime unload clears
all tables. No new per-frame scans, custom guest state or network messages exist.

## Dark Cloud

The attacker/player-pair strike limit defaults to **0.5 seconds** since `0.62.1`
(2026-10-18), increased from the 0.25-second setting in `0.55.2`. It remains
separate from the shared
70% HP/s default budget and is part of the balancing toggle. Since `0.68.0`, edit
it independently with `/one friendlyfire dot dark-cloud N|reset` or the interval
panel; live edits retain the last strike timestamp.
**When balancing is off, that limit is off too**, even if friendly fire
is on. All other Dark Cloud behavior remains as recorded in the
[artifact guide](friendly-fire-artifacts.md#dark-cloud-player-strike-limit-0512-2026-10-13).

When on, the whole repeated strike is skipped (damage, strike-triggered burn and
FX), including queued strikes after the owner dies. Existing cloud costs and
timers remain native. Turning balancing off/on with no intervening strikes changes
`CombatBalanceRevision`, so a previous cooldown cannot be reused. Existing weak
connection receipts, participant/KDA epochs and floor checks handle disconnects,
rejoins, selections, match rounds, restarts and resets without per-frame work.

## Debuff cadence (`0.54.2`)

Historical behavior below was superseded for Burn, Plasma Burn, Frostbite and
Poison by [independent intervals](pvp-dot-intervals.md) in `0.59.0`. Since `0.68.0`,
Wound also uses configured seconds (default 1.33s) instead of the old multiplier.

`CharacterDebuff_Burn.OnUpdate_Server` normally advances `tickTimer` by
`Time.deltaTime * (1 + BURNSPEED/100)`. Only for current permitted player victims
under effective FF plus balancing, `FriendlyFireDebuffBalanceHooks` wraps that
exact elapsed input with `FriendlyFireRuntime.DebuffTickDelta`, multiplying it by
0.75. A native 0.5-second tick becomes about 0.667 seconds; native burn-speed
bonuses remain proportional. Frame granularity and the game's reset-on-tick
timer can make observed intervals slightly longer.

The same registry also covers Plasma's burn timer, Frostbite, Poison and Wound:

| Effect | Audited native periodic timer | Adjustment |
| --- | --- | --- |
| Burn / Plasma burn | `tickTimer`, default 0.5s, native BURNSPEED | about 0.667s before speed bonuses |
| Frostbite | `frostDamageTimer`, default 1s | about 1.333s |
| Poison | `poisonTickTimer`, default 1s | about 1.333s; system poison exempt |
| Wound | `tickTimer`, default 1s | about 1.333s |

Intervals shown are constructor defaults; the multiplier applies to current
native timer input, preserving serialized settings. Plasma's separate shock
charge accumulator is not slowed. This does not edit timer intervals, prefab assets, duration, stacking, refresh,
blue-burn damage, on-death explosions, or the separate base debuff expiry timer.
Self/system effects and status-only effects retain native behavior. Player-owned
companion debuffs use the same ownership/participation rules. Each emitted tick
still uses shared damage softening, budget, shields, KDA and death handling.

The helper reads current policy each time, using the existing native update:
no extra scene scan, polling loop, network message, dictionary or restoration
journal. Disabling balancing immediately restores native elapsed input for
existing debuffs; it preserves partial progress rather than granting a new tick.
FF off, unselected players and Tournament bystanders retain native timer
progress while existing damage admission prevents allied harm. Existing debuff
receipt validation destroys stale connection effects before they can tick.
PvE damage and cadence are unchanged, as is stock client presentation.

The per-type installed-IL guards reject changed tick fields, multiple timer calls,
changed speed arithmetic or branches into the wrapped input. All other native
instructions are retained. Unload removes the hook with the existing FF patches.

This slows frequency, not per-tick strength. High speed/damage builds may
still saturate the new shared ceiling; the adjustment does not promise lower
total DPS for every build. Compare direct, Bolt, mixed-burn and dedicated-burn
builds in actual play before further tuning.

### Shock and Freeze pulses

Since `0.68.0`, these clocks are configurable independently as `lightning` and
`freeze`, retaining 0.25s defaults. Chim expiry damage has its own `chim` clock,
also 0.25s by default. Live edits preserve last-hit times, and all three use the
same receipt/budget lifetime described below.

Electric and Plasma shock damage runs through native `Attack` on refresh, expiry
and luck procs. Freeze damage runs on application and failed maximum-stack adds;
it has no periodic damage clock. Their exact native elemental damage IDs
(`Debuff_Electric`, `Debuff_Freeze`, from-type None), plus the native
`DEBUFF_ELECTRIC` flag for serialized Plasma shock (`Debuff_Plasma`), use independent
default 0.25s pulse clocks within the existing attacker/victim `BalanceWindow`. Plasma
and companion shock share their owner's shock clock. Direct lightning/Bolt hits
are not debuff pulses and do not share that clock.

The first admitted positive pulse reserves its clock before native
callbacks. Attempts within the interval resolve to zero damage; they consume no
tokens and are not queued or accumulated for a later burst. Native FX and other
callbacks still run. Since `0.77.0`, an exhausted-budget pulse deals the minimum
and reserves its interval even with zero funded charge. Shield/HP refunds return
unused funded damage tokens, not time; an admitted
pulse that later loses fractional damage to native MP rounding still counts as
a pulse. This deliberately limits damage cadence rather than status application.

Both clocks share the budget's receipt, combat/participant epoch, balance revision,
floor and life invalidation. Off/on, reconnect, run/session replacement, KDA reset,
match changes and successful revival cannot reuse stale clocks. No second player
registry, polling loop or guest state is added. Different attackers/victims and
Shock versus Freeze remain independent, subject to the same overall damage budget.

**Freeze still immobilizes players.** Native `CharacterDebuff_Freeze.ApplyDamage`
discards the damage result and then calls `StartStun(BuffDuration)`; only resolved
damage is limited. Native stun duration, immunity and Frostbite-to-Freeze stacking
are untouched, even if another Freeze damage pulse is currently suppressed. This
does not introduce crowd-control immunity or prevent repeated native stun refresh.
Since `0.68.0`, expiry-only Chim damage has its own configurable minimum gap.
Its charge accumulation/lifetime stays native. System death effects and
status-only debuffs remain outside the cadence policy.

## Harvest recovery (`0.57.2`, 2026-10-16)

Historical behavior below is superseded for HP by the
[2026-10-19 follow-up](#hp-steal-disabled-during-pvp-2026-10-19). MP still follows
the original balancing policy.

While effective friendly fire **and** PvP balancing are enabled, HP Harvest and
MP Harvest from permitted player-victim hits recover **20% of their native
amounts**. This uses the shared hit scope in casual friendly fire, Deathmatch,
Tournament and Play Tag, including reflected damage, debuffs, artifact procs and
player-owned companions attacking players. The actual native attacker receives
the recovery; a companion's recovery is not reassigned to its owner.

For example, if the already-resolved HP damage would normally yield 12.5 HP or
MP through Harvest, the recovery input becomes 2.5. Native healing penalties,
resource caps and MP recovery modifiers still apply. This scales the recovery
amount rather than rounding the integer Harvest stat: small MP gains accumulate
through the game's existing fractional `mpStack`. The native `C`-menu stats and
configured stat modifiers remain unchanged.

`FriendlyFireHarvestHooks` composes two amount helpers into the existing guarded
`UnitAvatar.ApplyDamage` transpiler. Native Harvest occurs after shields, actual
HP/overkill clamping and attack callbacks, before death. HP combines
`ECustomStat.HPSteal` and per-hit `DamageInstance.hpSteal`; MP uses
`ECustomStat.MPSteal`. Each rate divides by 1000, and both undo positive
`RECEIVEDDAMAGEINCREASE` before recovery. The native HP training-dummy exclusion
remains. Full installed-source search found no other stat-based Harvest grant
site; other occurrences define, write or display these stats.

Only the exact `Heal(float)` and `HealMpFloat(float)` inputs at those sites are
adjusted. Potions, shared Rabbit healing, regeneration, unrelated on-hit recovery,
self/system damage, PvE and non-player victims retain their existing paths.
Nested enemy hits cannot inherit a surrounding PvP hit's reduction. Guarded,
fully shielded and rejected hits retain their native no-Harvest behavior. The
shared participant/receipt/pair guards prevent excluded or stale players from
granting Harvest through a rejected attack.

Each recovery reads current effective policy. Disabling balancing or ending the
PvP session restores native effectiveness immediately, including a setting change
inside a native callback between the HP and MP grants. There are no temporary
stat writes, timers, caches, per-player overrides or preset fields to restore.
Existing hit finalizers handle nesting and exceptions; unload removes the entire
shared patch. Ordinary native HP/MP SyncVars deliver host calculations to stock
guests without new messages or guest addon state.

Verification uses the installed assembly SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
The initial combat regression failed with full Harvest and passed after the
change. Executable fixtures cover fractions, per-hit HP rates, native resource
modifiers, shields, lethal hits, reflection, debuffs, companions, artifact procs,
nested PvE, toggles inside callbacks, participant/pair changes, reconnects,
exceptions and unload. The warmed path allocated **0 bytes / 1,000 hits**.
Installed-IL checks verify only two amount helpers are added, native instructions
and labels are preserved, altered boundaries reject installation, and native
HP/MP replication remains in use. Release build (zero warnings/errors), 3,589
combat checks, 2,233 shared runtime checks plus 69 Bat lifecycle checks, the full
portable/installed-game suite and all 32 compiled Harmony API references against
co-installed Harmony 2.9 passed. Independent review found no actionable issues.
Live host/guest gameplay remains untested; nothing was deployed.

## HP Steal disabled during PvP (2026-10-19)

Active PvP now disables HP Steal from admitted friendly-fire hits, independently
of the optional balancing toggle. This applies to casual friendly fire and the
shared Deathmatch, Tournament, Play Tag and Zombie Survival combat paths,
including reflected damage, debuffs, artifact procs and player-owned companions.
Companion victims cannot provide an HP Steal bypass. Both `ECustomStat.HPSteal`
and the per-hit `DamageInstance.hpSteal` contribution are suppressed together
at their native HP grant; neither rate is changed.

MP Steal keeps its existing policy: 20% of the native amount on balanced,
permitted player-victim hits, and native recovery with balancing off or on the
existing companion-victim path. Fractional accumulation, native MP modifiers and
resource caps remain in `HealMpFloat`.

`FriendlyFireHarvestHooks` replaces only the HP Harvest `Heal(float)` call with
`HarvestHp(UnitAvatar, float)`. The wrapper checks current server authority and
the existing admitted-hit/participant/hostility policy immediately before the
grant. Native `Heal(float, bool, bool)` has no zero-amount early return: on an
injured living unit, zero still reaches `Networkhp` and
`OnHpChangedServerside`. Skipping the call avoids those recovery callbacks as
well as the health gain. The MP amount helper and every other native instruction
retain their existing order, labels and exception boundaries.

Native PvE, self/system damage and unrelated healing keep their paths. Each
nested hit owns its existing scope, so enemy hits inside a PvP callback still
receive native HP Steal and returning to the outer hit restores suppression.
Ending effective PvP is observed immediately, including during callbacks; there
are no stat overrides, timers, preset fields or restoration writes. A suppressed
HP grant cannot run a healing callback that would change the following MP grant.
Unrelated native heals can still run such callbacks, and MP reads the resulting
live balance policy.

The installed assembly still has SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510` and MVID
`ccf97024-28bd-48f1-9666-c768c2bd4b2b`. REA static identity evidence is
`ev_b2ff80fd487641fed1eca32253a16cab08bd0d6b79d5bd38673c81845959da36`;
selected `UnitAvatar` member evidence is
`ev_5d812c79cf8b4450b80a9299fff702fc763ea7301f381920b1bbb2965a45c57f`.
The cached matching decompilation supplied the heal control-flow observation.
REA did not execute the game; its member metadata coverage is complete for that
type, with separately reported malformed terminal flags in some user strings.

The new HP suppression regression failed before implementation. The executable
combat suite then passed **4,944 checks**, including native callback suppression,
stat/per-hit rates, both balance settings, companion origins/victims, reflected
and nested damage, debuffs, current Tournament/Zombie pairs, Tag modifiers,
reconnects, exceptions, stopping PvP and unload. The warmed Harvest path allocated
**0 bytes / 1,000 hits**. Release build with `-p:DeployMod=false` passed with zero
warnings/errors. Installed-IL checks cover the exact HP wrapper and MP insertion,
preserved native instructions, drift rejection and stock SyncVar transport.
The complete portable/installed-game contract suite passed, including these
Harvest contracts and **3,824** bundled localization checks.
Live host/guest gameplay remains unverified; nothing was deployed or launched.

## State, persistence and restoration

The balancing layer writes **no player stats, health limits, native bonuses,
inventory or pooled DamageInstance values**. Actual `MaxHp` is read on every
relevant hit; changing a costume, native menu, item or buff needs no stat resync. Invalid
nonpositive/nonfinite maximum HP produces zero balanced damage rather than invalid
HP/MP writes. The existing native integer-safety ceiling remains in force.

There are no modified stats to restore on off, end, cancellation, interruption,
reset, disconnect or unload. The temporary budgets use the lifetimes described
above. Match cleanup disables effective friendly fire as
before; the retained balance preference alone cannot alter damage. `/one
friendlyfire off` retains the preference; `/one friendlyfire reset` restores it
to off along with the other combat defaults. New sessions use defaults unless a
preset was explicitly saved. Enabled balancing uses preset v22; earlier versions
still parse and default balancing to off. Malformed/duplicate rows fail atomically.

## Verification and limits

`0.55.2` Dark Cloud tuning verification on 2026-10-15: the quarter-second
boundary test failed before the change and passed afterward. All 1,429 combat
checks and installed-game contracts passed; Release build had zero warnings or
errors. Tests retain PvE/balance-off behavior, independent pairs, queued strikes
after death, reconnect/reset handling and zero allocations per warmed strike.
No deployment or live gameplay validation performed.

`0.55.1` default tuning verification on 2026-10-15: Release build had zero
warnings/errors; 1,429 combat checks, 1,998 runtime checks plus 69 Bat checks,
137 refill checks, 3,375 catalog checks and all installed-game contracts passed.
Tests cover zero-initialized defaults, reset, old presets, explicit saved rates,
70% combat timing and continued support for configurable 100% subsecond kills.
No deployment or live gameplay validation performed.

`0.55.0` configurable refill verification on 2026-10-15:

- Release build with `-p:DeployMod=false`: zero warnings/errors.
- 1,427 friendly-fire executable hook checks, including configurable refill,
  subsecond full-health kills, same-frame edits, conservative rate transitions,
  MP-shield accounting and zero allocations per warmed balanced hit.
- 1,998 runtime/session checks plus 69 Bat checks; refill survives saved-policy
  restoration, run restart/rejoin and both match overlays without resetting KDA
  or interrupting an active match. Guest mutations are rejected.
- All portable and installed-game contracts passed, including 129 dedicated
  refill checks, 3,375 bundled catalog checks and compiled UI wiring checks.
- Code review found sibling numeric drafts were discarded on either Apply.
  A regression check failed before the fix; successful Apply now clears only
  its own field. Failed commands retain drafts; scope changes still clear both.
- No game launch, deployment or live multiplayer/visual validation performed.

`0.54.4` pulse tuning verification on 2026-10-15:

- The revised quarter-second test failed at the interval boundary before the
  timing change, then passed. Both Shock and Freeze reject attempts at 0.249s,
  admit at 0.25s and reject a second pulse on that same frame.
- 1,410 executable combat checks passed, including MP-shield rounding, native
  Freeze stun, PvE/balance-off exclusions, lifetime resets and warmed allocation.
- Release non-deploying build passed without warnings/errors. Portable/native
  contract and localization checks passed, including 3,357 catalog checks.
- English and Korean help describe the new interval. No game launch/deployment;
  live multiplayer pacing remains unverified.

`0.54.2` cadence verification on 2026-10-15:

- Debug and Release non-deploying builds: zero warnings/errors.
- 1,408 combat hook checks: direct/magic refill, shields/refunds, existing
  connection/life resets, burn timing, speed bonuses, mid-effect toggles, native
  PvE cadence, selection/Tournament exclusions, companion ownership, stale
  reconnects, pause, unload, all five periodic timers, Shock/Freeze pulse resets,
  preserved stun, and zero warmed allocation in timer and pulse helpers.
- 1,983 runtime/session checks and 69 Bat lifecycle checks passed.
- Portable/native checks passed, including 3,357 catalog checks, exact burn
  tick rewrites and mutation rejection, preserved expiry/stun, and 30 Harmony API
  references checked against co-installed HarmonyX 2.9.0.0.
- The new burn and refill tests were observed failing before production changes.
  Expanded periodic and Shock/Freeze tests also failed before their additions.
  Independent review found no actionable correctness issues.
  No deployment or game launch; live host/guest balance remains unverified.

`0.53.0` rapid-hit verification on 2026-10-14:

- Both non-deploying builds passed without warnings/errors.
- 1,313 executable combat hook checks passed, including rapid bursts/refill,
  slow attacks, nested procs/reflection, actual MP/shield/HP accounting, super
  armor, overkill/additional life, stale nested refunds and connection/life resets.
- 1,920 runtime/session checks and 69 Bat lifecycle checks passed. The runtime
  suite tests match/connection lifetimes separately; full damage/budget hooks run
  in the combat executable rather than its attribution-only death fixture.
- Portable/native compatibility and catalog suites passed, including the
  shared pre-shield budget hook, native revival and damage-finalizer wiring,
  shield/MP/HP native accounting contracts and 28 compiled Harmony API references
  against co-installed HarmonyX `2.9.0.0`.
- The warmed full balanced-hit fixture allocated zero bytes per hit. First use
  allocates weak actor/pair state; no new scene scans or per-frame update pass.
- Independent review found no blocking correctness issues. Its refund/life test
  and documentation gaps were addressed, including super-armor saturation.

Executable tests cover scale invariance across maximum HP values, monotonicity,
finite extremes, weak/strong hits, fractional host percentages, shields, defenses,
reflection, companion damage, debuff/triggered damage, invalid input, no mutation,
bounded same-frame repeated hits, selection exclusions and live changes.
Runtime tests cover saved presets, restart/rejoin, scope replacement, host-only
commands, unavailable-hook recovery, both match overlays and retained K/D/A.
Native compatibility tests retain the pre-shield scale boundary and check native
maximum-HP reads and shared UI command wiring. Warmed arithmetic and cloud checks
allocate no per-hit memory in the fixture; no new polling or network path is added.

Initial `0.52.0` verification on 2026-10-13:

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
