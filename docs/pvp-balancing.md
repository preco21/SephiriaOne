# Optional PvP balancing

Added in `0.52.0`, 2026-10-13. Host-only installation; the existing native damage
and FX replication continues to serve stock guests. Default: **off**.

Updated in `0.60.0`: [optional hit-frequency scaling](pvp-hit-frequency.md) adds
a separate default-off checkbox/command. Above roughly four hits per second per
opponent, it progressively reduces per-hit damage before the existing budget.
Both mechanisms share pair lifetimes; frequency toggles do not refill tokens.

Updated in `0.59.0`: [independent DoT intervals](pvp-dot-intervals.md) replace the
earlier cadence rule for Burn, Plasma Burn, Frostbite and Poison. Each defaults
to 0.75 seconds and has separate live controls, commands and saved settings.

Updated in `0.59.2`: [Burn's two-second lifetime cap](#burn-duration-cap-0592-2026-10-16)
caps each balanced PvP Burn application or refresh, regardless of duration bonuses.

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

Enable **Combat → PvP balancing** (`PvP 밸런스 보정`) or use:

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

## Burn duration cap (`0.59.2`, 2026-10-16)

While balancing and positive-damage friendly fire are enabled, normal and blue
**Burn expire within two seconds of their latest application or native refresh**,
at the first server update at/after the deadline. General debuff-duration bonuses
and `BURNDURATION` cannot increase this window. A new application to an active
Burn may refresh it for up to two more seconds, including at maximum stacks.
Non-renewing stacks keep their native behavior and do not reset the deadline.
Repeated successful applications can therefore sustain Burn; after the last
application, its remaining lifetime is at most two seconds. Native durations
shorter than two seconds remain shorter. The independent Burn tick interval and
shared damage budget are unchanged. Without refreshes, a Burn configured with a
tick interval of two seconds or longer can expire before its first periodic tick.

The cap uses the current permitted player pair in casual friendly fire,
Deathmatch, Tournament and Play Tag. Player-owned companion Burn uses the same
owner receipts. PvE in either direction, self effects, non-player victims,
unselected players and disallowed Tournament pairs retain native expiry.
Shared damage rules still block nonparticipating targets. **Plasma Burn is a
different native type and is not included in this Burn-only change.**

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
Burn inherits this method. The refresh happens before the maximum-stack check,
so applications at maximum stacks can still renew the duration.

`FriendlyFireBurnDuration` reuses `FriendlyFireDebuffLifetime`'s existing weak
origin record, with the latest application time and an ended flag. A postfix on
the native refresh updates that timestamp only for valid, live Burn receipts.
It does not reset periodic tick progress. The validated pre-update path
checks the two-second deadline before native damage and calls native `Destroy`
once. Native cleanup removes status, calls removal callbacks and schedules the
normal stock-client despawn. No prefab, native timer configuration, stat or
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

Executable coverage reproduces the former long-duration Burn, then checks the
exact two-second boundary, large bonuses, direct refresh, stacks, short native
expiry, delayed-despawn damage, new applications/reinitialization, live toggles,
PvE/owner/selection/match exclusions, companion death, reconnection, pause,
exceptions and unload. Warmed duration checks allocate zero bytes in fixtures.
Installed contracts verify the native duration/refresh ordering, blue-vs-Plasma
identity, native cleanup/network despawn and shared pre-update integration.
Release build passed with zero warnings/errors; 3,936 combat checks, 2,254 runtime
checks plus 69 Bat lifecycle checks, installed-game contracts and the 33-reference
Harmony 2.9 API audit passed. Independent review found no actionable issues.
Actual Unity/stock-guest timing and visuals still need live testing; nothing
was deployed or launched.

## Guard MP cost (`0.58.0`, 2026-10-16)

While balancing is enabled, blocking an admitted PvP hit costs **10% of the
defender's current maximum MP**, before native reductions. Guard Efficiency,
perfect guard's half-cost and native item/cost callbacks still apply. At 1,000
maximum MP, an ordinary block costs 100 MP, a perfect guard 50 MP; 40% Guard
Efficiency lowers those to 60 and 30 MP. This is charged on a successful block,
not simply on pressing or holding right-click.

Use **Combat → PvP balancing → Guard MP (%)** (`방어 MP (%)`) or:

```text
/one friendlyfire guard-mp 10
/one friendlyfire guard-mp 7.5
/one friendlyfire guard-mp reset
/one save
```

Range: 0..100%, up to two decimals, optional `%` suffix. Zero explicitly waives
the balanced guard cost. Positive fractional MP costs round up; a genuinely
zero native cost remains zero. Reset restores 10% without touching other
preferences, scores, participants or the current match. Full friendly-fire reset
also restores 10%. Saving a nondefault value uses preset v27; all older supported
presets inherit 10%. Default values add no extra preset row/version requirement.

The Combat panel has compact **Combat** and **PvP balancing** subsections. The
balancing toggle, refill and guard editors are together; K/D/A reset, Revive All,
the master toggle and participant selection remain accessible in both sections.
Each numeric editor preserves its own draft until Apply or a page/scope change.

Implementation uses the existing authoritative `ApplyDamage` hit scope. Only the
final float-to-int conversion before the guard branch's `UseMp` is replaced:
`ceil(nativeModifiedCost / 10 * MaxMp * configuredPercent / 100)`. It reads both
current policy and maximum MP after native callbacks. No stats are rewritten,
no guard cost is cached, and no restore pass, polling or network message is added.
Native `UseMp` retains MP callbacks, SyncVar replication for unmodified guests,
infinite-MP handling and the subsequent guard break. Separate parry and MP-shield
paths remain native. There is no scaled spending for PvE, self/system damage,
non-player victims, excluded participants or Tournament bystanders. Owned
companion attacks against permitted players use the same guard rule.

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

## Model and trade-offs

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
power, not every possible build advantage. Small hits are not buffed, and very
low host damage percentages can still produce negligible damage; try 100% as a
starting point when evaluating balancing. The setting never changes that slider
automatically or introduces a minimum damage floor.

## Rapid-hit budget (`0.53.0`, configurable in `0.55.0`)

Each directed attacker/player-victim pair has a token budget with **20% of actual
victim maximum HP capacity** and **70% of maximum HP per game second refill by default**.
Refill is continuous, pauses with the game clock and stops at capacity. All of an
attacker's weapons, artifact projectiles/procs, debuffs and companions share the
same pool against that victim. Changing damage IDs, weapons or projectile objects
does not grant another pool. Other attackers and other victims remain independent;
reflection uses the reverse directed pair. Dark Cloud retains its separate limit.

For fixed maximum HP `H`, applied HP plus regular/MP-shield damage from one
attacker over `T` seconds is bounded by approximately `H * (0.20 + R*T)` from
a full pool. Thus an initially full-health, unshielded target cannot be killed
by that attacker in less than about **1.14 seconds at the default** through this
damage path (`R = refill percent / 100`; minimum time is `0.80 / R`). This
is a ceiling, not guaranteed DPS or a minimum-damage boost. Actual hits, defenses,
healing and shields can make fights longer; multiple attackers remain independent.
Setting refill to 100 permits subsecond kills when enough damaging hits land. The budget gives
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
the budget. No damage debt is delayed and emitted later. Zero-budget hits can
deal zero damage while native non-damage callbacks still run. This is not a
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

The attacker/player-pair strike limit is **0.25 seconds** since `0.55.2`
(2026-10-15), reduced from 0.5 seconds. It remains separate from the shared
70% HP/s default budget and is part of the balancing toggle.
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

Historical behavior below is superseded for Burn, Plasma Burn, Frostbite and
Poison by [independent intervals](pvp-dot-intervals.md) in `0.59.0`. Wound keeps
this earlier rule.

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

Electric and Plasma shock damage runs through native `Attack` on refresh, expiry
and luck procs. Freeze damage runs on application and failed maximum-stack adds;
it has no periodic damage clock. Their exact native elemental damage IDs
(`Debuff_Electric`, `Debuff_Freeze`, from-type None) therefore use independent
0.25s pulse clocks within the existing attacker/victim `BalanceWindow`. Plasma
and companion shock share their owner's shock clock. Direct lightning/Bolt hits
are not debuff pulses and do not share that clock.

The first nonzero pulse with available budget reserves its clock before native
callbacks. Attempts within the interval resolve to zero damage; they consume no
tokens and are not queued or accumulated for a later burst. Native FX and other
callbacks still run. Zero charge and exhausted-budget hits do not reserve a new
interval. Shield/HP refunds return unused damage tokens, not time; an admitted
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
Expiry-only Chim damage, system death effects and status-only debuffs are outside
this cadence policy and retain their existing behavior.

## Harvest recovery (`0.57.2`, 2026-10-16)

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
