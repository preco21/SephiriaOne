# Development knowledge index

[Bolt Magic projectile limits](projectile-limits.md#bolt-magic-coverage-0960-2026-10-26):
manual/automatic/staff Bolt casts share one source across elements, existing
active/rate admission and lifecycle cleanup. Both native branches have safe
post-spawn guards; in-flight adoption, command/UI/presets and EN/KO labels covered.

[PvP startup compatibility fix](harmony-compatibility.md#pvp-initialization-and-dark-cloud-observer-fx-0952):
the Dark Cloud observer adapter used a helper absent from co-installed HarmonyX,
disabling friendly fire. Framework delegate binding and a caught non-inlined
installer preserve optional-FX isolation; exact-library API audit passes.

[Settings UI fixes](tabbed-settings-ui.md#scroll-layout-and-dropdown-cursor-fixes-0951-2026-10-25):
equal-width scrollbar arrows/track, whole-rail auto-hide, padded scrolling forms,
and projectile readout separated from controls. Addon dropdowns draw below the
native cursor; installed canvas/blocker semantics checked, live input pending.

[Grouped settings window](tabbed-settings-ui.md#grouped-navigation-and-rounded-controls-0950-2026-10-25):
seven sections in a vertical rail, exact EN/KO labels, drawn icons, rounded
surfaces, subtle control borders and shared scrollbar arrows. Stable commands,
snapshot scopes and input ownership; no per-frame presentation work.

[Dark Cloud PvP observer FX](dark-cloud-pvp-visuals.md): host forwards the native
lightning/ice/EX strike to ready same-floor cloud observers, including guests
without the addon. Owner FX/audio stays single; the range ring remains owner-only.
Scoped routing and installed-game contracts verified; live guest rendering pending.

[Rabbit infinite-potion PvP override](pvp-potions.md#rabbit-costume-override-0940):
default-on Combat checkbox and command prevent costume retention from bypassing
finite PvP potions. Shared participant admission, callback/death guards and preset
v53; general PvP infinite HP retains priority. No inventory snapshot restoration.

[Zombie movement penalty](zombie-survival.md#zombie-movement-penalty-0931): Zombies
walk/run at 80% through the native base-speed SyncVar, with role-owned restoration,
existing roster lifecycle and no repeat writes. Other movement modifiers remain
native; includes EN/KO status and stock-client contract checks.

[PvP elemental stat limits](pvp-elemental-limits.md): default-on independent
50/100 caps share the amplification curve. Effective participant-stat fallback
also affects their simultaneous PvE damage; saved/derived stats and C-menu reads
stay native. Includes creation-time attack snapshots, preservation hooks, EN/KO
controls, v52 presets and allocation/native compatibility checks.

[Ice Weapon targeting recheck](ice-weapon-targeting-audit.md): Deathmatch report,
native activation versus homing/manual steering, shared participant/lifecycle
coverage and limiter admission as an unconfirmed explanation. Added regression
and installed-game checks; no confirmed gameplay defect or behavior change.

[Periodic PvP recovery](pvp-hit-frequency.md#periodic-recovery-0920-2026-10-24):
default on/1s, distinct from the rolling hit-history window. Restores shared
pair burst/history during continuous fire; lazy fixed deadlines, array reuse,
live-edit rearming, nested-refund protection and EN/KO controls/preset v51.

[Zombie majority refill tuning](zombie-survival.md#dynamic-balancing-0760):
`0.91.1` lowers dynamic refill smoothly as Zombies exceed half the roster
(3:1 → ×0.75), retaining early-Zombie bonuses, incoming curves and existing
time smoothing without extra combat-loop work.

[Secondary emission controls](projectile-emission-controls.md): four additional
Ice Weapon families, independent off/64-player/s/12-source/s creation budgets and
rate-only Plitvice/Guillotine/Frost Flame Sword paths. Global FIFO is deliberately
not enabled after native teardown, guest-lifetime and churn risks were confirmed.

[Host-only capabilities](host-only-capabilities.md): guest-visible gameplay and
presentation survey, including initial versus live player scaling, native skins,
FX/audio selectors, pings, persistent markers and weather. Distinguishes replicated
state, transient RPCs, host decisions and local-only edits; new candidates are
research, not implemented or live-tested features.

[Projectile/effect performance audit](projectile-performance-audit.md): full Ice
Weapons category and variant coverage gaps, RPC/local effects, client trails,
child provenance and spawn-throughput limits. Includes a measured counter-storage
optimization in 0.90.3 and prioritized follow-ups; guest stutter cause unproven.

[Respawn countdown emphasis](overhead-message-transport.md#respawn-countdown-emphasis-0902-2026-10-23):
native cyan floating respawn text uses 125% size and bold markup. Chat/bubbles
remain for comparison; the bitmap-font limitation and stock-guest evidence are
documented. Bomb notices and countdown frequency are unchanged.

[October disconnect audit](disconnect-projectile-audit.md): observed Steam timeout
after failed sends, projectile lifetime/ownership review, separate stale FrostTouch
FX fix, and throttled per-recipient transport diagnostics. Distinguishes evidence
from unproven congestion/client-stall causes; host/guest reproduction remains open.
The v0.89.0 final-boss-entry follow-up finds failed sends already logged before
boss initialization and no room-entry projectile reset; later combat changes
are excluded from that reported version.

[PvP lower-bound assistance](pvp-lower-bound.md): optional opponent-HP-relative
uplift for weak hits, default off / 4% reference / 8% bonus refill per second.
One shared bonus pool across every source retains rapid-hit, upper-budget, role
and shield limits. EN/KO controls, v49 presets, lifecycle and performance checks.

[Planet PvP damage audit](pvp-planet-damage.md): each audited Planet type now has
independent rapid-hit history while all sources retain one shared damage budget.
Includes native Planet/Ice Weapon/Fire Arrow formulas, Zombie-role damage trace,
serialized IDs, lifecycle/performance checks and live verification limits.

[Independent Dark Cloud interval](pvp-autonomous-artifacts.md): the general
autonomous-artifact minimum no longer overrides Dark Cloud's dedicated interval.
Live general-setting edits preserve its strike clock; saved values are retained.

[Optional PvP Thorns suppression](pvp-balancing.md#optional-thorns-suppression-0890-2026-10-22):
off by default, with an EN/KO PvP Balance checkbox, shared host command and v48
presets. Blocks the exact native Thorns return against hostile players before
callbacks/costs, across every PvP mode; PvE and other reflections stay native.

[Dedicated Projectiles tab](control-panel.md#dedicated-projectiles-tab-0881-2026-10-22):
all limiter controls now live beside Spawns in their own EN/KO tab. Spawn controls,
projectile policies, saved values and shared command/lifecycle behavior remain unchanged.

[PvP shared-burst fix](pvp-burst-audit.md): removes the low per-hit curve and
raises default stored reserve to 75% max HP. Slow heavy hits and rapid packets
share one recoverable allowance; refill remains independently configurable.
Includes before/after diagnostics, saved-setting behavior and validation limits.

[Teleport all](teleport-all.md): Combat's **Teleport all players** button and
`/one teleportall` request native party movement
to the host for ready, living players on the same floor, with connection/run guards
and stock guest RPCs. No polling or PvP dependency.

[Lobby dice grants](lobby-dice.md): dice settings now adjust an already-created
pre-run lobby through shared commands, inheritance and maintenance. Spent balances,
reconnect checkpoints and future-only mid-run edits remain protected.

[Autonomous artifact PvP cadence](pvp-autonomous-artifacts.md): optional hard hit
interval, default off / 2s, for seven audited automatic artifact sources.
Dark Cloud uses its own interval. Shared pair/life/session clocks, explicit weapon/book/companion/planet
exclusions, native guest damage sync, EN/KO controls and v47 presets.

Maintained: 2026-10-24 (Asia/Seoul), addon `0.94.0`.

[Configurable PvP DoT duration](pvp-dot-intervals.md#configurable-duration-cap-0850-2026-10-22):
Burn/Plasma/blue/evolved lifetime cap now defaults to 1.2s, adjustable 0.05..10s
in PvP Balance or `/one friendlyfire dot-duration`. Shared live age/refresh
semantics, native PvE, v46 presets and EN/KO controls; interval coverage is unchanged.

[Perfect Guard MP multiplier](pvp-shield-mp.md#perfect-guard-multiplier-0840):
optional native-cost x1..x100, default off/x2, in PvP Balance and chat commands.
When enabled it is exempt from the shield minimum, while other shields retain
their rules. Shared PvP mode/connection lifetime, v45 presets and EN/KO controls;
no stat writes or per-hit allocations. Refill remains in Rapid Hits.

[Configurable rapid-hit balancing](pvp-hit-frequency.md#controls): dedicated
Rapid Hits tab, separate detection window/rate/strength and continuous
capacity/refill controls, EN/KO explanations and v44 presets. Live edits
preserve matches, K/D/A and damage credit. No periodic reset,
per-hit allocation or polling is added.

[Dedicated PvP Balance tab](control-panel.md#dedicated-pvp-balance-tab-0820-2026-10-21):
general balancing left, taller debuff/Dark Cloud list right; Combat retains
supplies and participants. Wider tabs, more vertical space, no new polling or
gameplay changes; the new page skips participant-detail snapshots.

[Additional PvP defensive effects](pvp-defensive-effects.md): Elastic Band
refunds at most once per defender every 2s; extended katana/dagger counter windows
cap at 0.35s per activation against players while balancing is enabled. Native
PvE protection stays intact. Includes retained finite defenses, disabled content,
asset evidence, shared lifecycle and live-verification limits.

[PvP shield MP minimum](pvp-shield-mp.md): strict default 5% after native
reductions, covering guards, parries, MP shields and automatic/dash shields.
Layered shields pay once; automatic shields preserve native absorption with
depleted MP. Includes the Iron Wall audit and replaces guard-only semantics.

[PvP All Damage Amplified limit](pvp-amplification.md): optional smooth bonus
curve, default off with soft 50%/hard 100%, in PvP Balance or
`/one friendlyfire amplification`. Limits the native combined attacker/companion
owner contribution only for eligible balanced PvP hits; no stat writes or PvE
changes. Includes v43 presets and English/Korean controls.

[Secondary projectile limits](projectile-limits.md): optional host-side PvE/PvP
caps for Pallas, Liberation and audited secondary sources. Spawns → Secondary
projectiles or `/one projectiles`; default off, 64/player and 12/source with
overrides. Existing instances finish; ordinary attacks and Minigun remain native.
Includes v42 presets, EN/KO, lifecycle accounting and explicit coverage boundaries.

[Large-MP guard correction](pvp-balancing.md#large-mp-guard-correction-0782):
balanced PvP guard costs widen the native Final MP multiplication and addition,
so extreme pools cannot wrap into a smaller or zero percentage cost. Ordinary
rounding and PvE behavior remain. Guard reductions are now bounded by the floor
described above. The exact reported
nine-Aquamarine scenario still needs live confirmation.

[Minority-side Zombie balancing](zombie-survival.md#dynamic-balancing-0760):
neutral parity, modest minority-Zombie help and stronger minority-Human offense
and protection. Zombie refill ×0.5..1.5, incoming 80..160%; Human incoming
60..100%. Existing smoothing, toggles and manual settings remain; EN/KO status
and the Zombie panel show the Human protection value.

[PvP infinite-potion correction](pvp-potions.md): Small/Large HP drinks now
combine retention, zero MP cost and Survival random-stat suppression for active
participants, including Rabbit. Sample/other potions retain native behavior;
the existing toggle and shared mode/connection lifetime still control eligibility.

[Responsive PvP damage and cadence](pvp-hit-frequency.md): direct attacks and
incidental damage have separate hit-rate histories with one shared allowance.
Exhausted credit retains a positive pre-shield minimum; funded-only refunds
prevent free credit. Extreme-rate minimum hits can exceed the refill limit.
No new toggle, preset version or per-frame task; native effect schedules remain.

[Zombie Dynamic Balancing](zombie-survival.md#dynamic-balancing-0760):
optional team-ratio curve eases across conversions. Toggle in Zombie or
`/one zombie dynamic on|off|reset`, default off.
Manual values and individual switches remain available; v41 saves the toggle.

[Heaven/Requital Slates](requital-slates.md): Items → Starting Slates now selects
either native Slate for automatic loadouts and Give now.
`/one slates type heaven|requital|reset`; v40 saves Heaven selection. Changing type
retracts old automatic IDs, while ordinary/manual gifts remain untouched.

[Tournament opponent status](tournament.md#live-opponent-status-0740-2026-10-20):
the active pair sees opponent/time bubbles refreshed once per second in EN/KO.
Deaths, pairing changes and stops retire the clock through native replacement
notices; empty chat cannot clear a stock guest's bubble. Shared respawn and
connection lifetimes prevent stale messages.

[XP restriction](experience.md): Progression-tab toggles block new XP for all
hosted-session players, with an optional Wing-Eared Rabbit exception. Both default
off; `/one xp block on|off`, `rabbit-exempt on|off`, `status`, `reset`; v39 saves them.
Existing levels and XP remain intact; changes apply at the next native XP grant.

[Starting Slates](requital-slates.md): Items-tab automatic starting loadout
(default off, 1 per player, configurable 0..20), plus a live Give now button.
`/one slates auto on|off`, `count N`, `give`, `reset`; Requital remains the default.
Automatic gifts are tracked by exact instance ID for retraction.

[Infinite PvP HP potions](pvp-potions.md): separate default-on
toggle in Combat → Supplies or `/one friendlyfire infinite-hp on|off|reset`.
Active combat participants retain regular/Large HP potions with native self-healing;
Sample/MP potions and Rabbit-only mechanics remain separate. Shared participation,
receipt and guarded drink infrastructure cover every current mode; v37 saves it.

[PvP Large Potion supplies](pvp-potions.md): host command/button grants one to
each player who lacks it. Optional automatic supply follows the shared PvP
participant roster on entry/match start and successful respawn; default on.
Only missing potions are replaced. Consumption while alive and loading do not
refill supplies. Explicit opt-out is saved in v36 presets.

PvP guard: the default/reset is a strict **5% of maximum MP** after reductions,
including automatic shields. Explicit saved overrides remain unchanged; see
[current shield MP semantics](pvp-shield-mp.md).

PvP reapplication: [Keep schedule / 주기 유지](pvp-dot-intervals.md#preserve-schedules-0690)
is independently configurable for nine debuff families, enabled by default.
The first native immediate hit remains; repeated applications cannot bypass the
configured interval. Native stacks, duration refreshes and Freeze stun remain.
Commands, UI and presets use the existing shared combat policy.

PvP intervals: [all audited debuff damage families and Dark Cloud](pvp-dot-intervals.md)
have independent controls. Defaults: Lightning/Shock 0.5s, Plasma Burn and Plasma
shock 3s each, Dark Cloud 2s, Wound 1.33s, Freeze damage 0.25s and Chim damage 0.25s.
Scroll within Combat → DoT. v37 separates shock overrides while migrating explicit
older shared values to both effects; custom saved intervals are preserved.

PvP damage: [positive scaled hits have a one-point minimum](pvp-balancing.md#minimum-positive-damage-0671-2026-10-20)
before native shields. Zero/invalid/vetoed hits and exhausted budgets remain zero;
fractional results above one remain unchanged. Minimum hits debit their full
amount from the shared damage budget, including incoming role reductions.

Rabbit settings: [PvP shared-healing restriction](rabbit-potions.md#pvp-sharing-restriction-toggle-0670-2026-10-19)
is now a host toggle, default on, in the Rabbit tab or `/one rabbit block-pvp-share
on|off`. Opt-outs save in v33; old saves/reset keep the restriction on. Live command,
status, tooltip and role notices reflect the option in English/Korean.

PvP artifact rule: [White Bread](pvp-white-bread.md) loses its added post-hit
invulnerability time for active combat participants in every mode, independently
of optional balancing. Native base/fall/dodge/revive protection remains; native
artifact behavior resumes immediately when PvP ends. No guest addon is needed.

Zombie visibility: [the persistent green X marker](zombie-role-visual.md#green-x-implementation-0662-2026-10-19)
replaces subtle Poison particles using the native Chim sprite loop. Owned
carriers cannot enter native expiry damage; normal debuffs stay unchanged.
Existing role/reconnect/cleanup handling and countdown cadence remain intact.
Stock-client contracts pass; live combat readability still needs verification.

Zombie notices: [faction markers and conversion bubbles](zombie-survival.md#faction-and-conversion-notices-0661-2026-10-19)
use font-safe `[Z]` / `[H]`, name every initial Zombie, and send opening role
bubbles only to them. Conversion combines infector, actual revival time and
Zombie instructions into one bubble without generic respawn/floating overlap.

Zombie setup: [starting Zombie count](zombie-survival.md#starting-count-0660-2026-10-19)
is configurable in the Zombie tab or `/one zombie count N|reset` (default 1).
Each match selects distinct entrants randomly and clamps to leave one Human.
Edits apply to the next match; v32 saves the requested count, never teams.

PvP recovery: [Rabbit shared healing](rabbit-potions.md#pvp-recovery-exclusion-0652-2026-10-19)
is blocked from/to active combat participants and [HP Steal](pvp-balancing.md#hp-steal-disabled-during-pvp-2026-10-19)
is disabled on friendly-fire hits, even with balancing off. Shared live mode and
participant rules restore PvE behavior immediately. Burn now defaults to 0.9s;
Zombie incoming damage defaults to 30%. Explicit saved values remain intact.

Lifecycle fix: [repeated addon loading and lobby combat](addon-reload-and-lobby-combat.md)
traces duplicate hook failures to native loader rescans without unloading. Shared
entry ownership prevents stacked hooks/controllers and preserves correct cleanup.
Nine-player fixtures cover all combat modes and independent damage budgets;
intentional throttling and the native fractional MP-shield limitation are documented.

Zombie presentation: [persistent role marks](zombie-role-visual.md) reuse the
native Chim loop as a separately owned cosmetic carrier. Current Zombies keep
it through respawn; shared match cleanup removes it. No gameplay debuff is applied.
Native stock-client transport is checked; live multiplayer rendering remains unverified.

Team mode: [Zombie Survival](zombie-survival.md) reuses shared participants,
countdown, one-second recovery, hostility, K/D/A and role modifiers. Zombie kills
convert Humans; an optional any-death toggle defaults off. Unconverted Humans
survive timeout even while awaiting revival. Commands and a compact EN/KO tab
configure duration and independent Zombie bonuses. Active teams never persist.

Investigation: [host-only cutscene acceleration](cutscene-speed-investigation.md)
found server-controlled progression but client-local playback clocks. The native
paths inspected cannot provide uniform 3x–5x playback on stock guests; selected
shorter pauses or native boss skips remain narrower candidates. Research only.

Prototype: [floating countdown text](overhead-message-transport.md#floating-countdown-prototype-0630)
adds native cyan text for respawn seconds and the Play Tag bomb timer while
retaining every existing chat/bubble notice. Native transport and fixture checks
pass; live host/guest readability and delivery remain unverified.

PvP tuning: [Dark Cloud](pvp-balancing.md#dark-cloud) now admits one strike per
attacker/opponent every 2 seconds by default while balancing is enabled. Other effect
intervals, the shared damage budget and native enemy behavior are unchanged.

Costumes: [current role summaries](costume-role-notices.md) privately describe
Rabbit healing and companion bonuses on equip/re-entry, using only enabled,
applied settings. Native chat supports stock guests; EN/KO text refreshes without
replaying gameplay or replacing combat bubbles.

Play Tag: [configurable rounds](play-tag.md#configurable-rounds-0610-2026-10-18)
adds a compact panel field and `/one tag rounds N|reset` (1–100, default 1).
Each explosion ends a round; recovery and a new countdown retain participants
and K/D/A, while bomb state resets. Settings apply to the next match and save in v30.

Combat fix: [remaining projectile hostility paths](friendly-fire-projectile-audit.md)
extend existing homing to verified crossbow, wand, Dark Veil, Formless Dance,
Adamah and enhanced Mini Ballista projectiles. Scoped weapon searches and live
mine proximity checks share existing participant/mode policy and native transport.

Combat balancing: [Evasion's 50% cap](pvp-balancing.md#evasion-cap-0604-2026-10-17)
limits stat-derived dodge chance before attacker anti-Evasion during eligible
PvP hits. Low chances, guaranteed item dodges, native stats/UI and PvE remain
unchanged. It uses the existing balancing toggle and shared match policy.

Combat fix: [Liberation projectile homing](friendly-fire-liberation.md) adds its
four elemental bullets to shared hostile-player acquisition and target loss,
using exact prefab and damage identities. Native enemy targeting and stock
guest movement replication remain unchanged.

Performance follow-up: [diagnostic state labels](performance-review.md#diagnostic-state-labels-0602-2026-10-16)
avoid repeated enum boxing in full Status/disconnect reports while preserving
live localization and output. The five-player full Status fixture allocates
about 3.2% less; unchanged synchronization remains allocation-free in fixtures.

Combat balancing: [optional hit-frequency scaling](pvp-hit-frequency.md) adds a
default-off checkbox/command that reduces rapid hits above roughly four per
second per opponent before the existing damage budget. Shares its lifetimes,
with an independent toggle revision that cannot refill the budget.

[Low-scale PvP fairness audit](pvp-hit-frequency.md#low-scale-fairness-audit-2026-10-20-0730):
explains intentional zero-budget hits, fractional damage display, the one-point
minimum's rapid-hit bias, and slow-attack compression. Includes controlled
comparisons and proposed fixes; gameplay behavior is unchanged by this audit.

Combat balancing: [Burn's two-second lifetime cap](pvp-balancing.md#burn-duration-cap-0592-2026-10-16)
now includes Plasma Burn and both families' Blue Flame/evolved variants. It uses
the existing lifecycle to cap each application and refresh, prevent late despawn
ticks, preserve Plasma's native expiry shock, and keep PvE native.

Combat fix: [Plitvice and secondary sword hostility](friendly-fire-artifacts.md#plitvice-and-secondary-swords-0591-2026-10-16)
repairs the spawned sword's collision veto and Flame Sword auto-search using the
shared participant policy, with an expanded Ember/Frost/secondary-effect audit.
The [local-installation recheck](friendly-fire-artifacts.md#recheck-against-the-local-installation-2026-10-16)
found the game still installed/loaded `0.57.0`, which predates those fixes.

Agents and contributors can refer to `docs/*` for research, implementation
materials, native game findings and references. Start here before repeating an
investigation. Repository workflow and deployment rules are in [AGENTS.md](../AGENTS.md).

## Start here

Combat balancing: [independent DoT intervals](pvp-dot-intervals.md) give Burn,
Plasma Burn, Frostbite and Poison independent defaults and live controls/
commands and v28 saved settings. Native PvE, Wound and separate Shock/Freeze/
Dark Cloud limits are preserved.

Combat balancing: [guard MP scaling](pvp-balancing.md#guard-mp-cost-0580-2026-10-16)
defaults to 10% maximum MP before native Guard Efficiency/perfect reductions.
Live command and dedicated balancing controls share settings across all modes;
native PvE, parries and MP shields are unchanged. Nondefault saves use v27.

Combat recovery: [HP Steal and MP Harvest](pvp-balancing.md#hp-steal-disabled-during-pvp-2026-10-19)
use separate recovery rules: no HP Steal on friendly-fire hits; MP Harvest stays
at 20% on balanced player hits. Native stats, PvE and fractional MP accumulation
remain unchanged.

Combat fix: [companion penalties](friendly-fire.md#companion-penalty-exemption-0571-2026-10-16)
exempts admitted friendly-fire companion hits/deaths from party crime, saved
crime counts and global hostility, preserving normal NPC penalties and cleanup.

Combat follow-up: [Play Tag](play-tag.md) adds a bomb carrier, shrinking fuse,
one-second respawns and finite minimum-fuse holdings to the shared match runtime.
It includes native Leaf Explosion transport and a separate settings tab.
`0.57.0` adds separate, initially enabled chaser bonuses: x2 global PvP refill
and 75% incoming player damage, using the shared combat boundary and v26 presets.
See [configuration, lifecycle and budget accounting](play-tag.md#chaser-advantages-0570-2026-10-16).

Performance follow-up: [resource planning and combat names](performance-review.md#resource-targets-and-combat-names-0553-2026-10-15)
removes unused successful-plan error formatting and plain-name buffers without
changing resource arithmetic, displayed names or combat/session checks.

Audio follow-up: [healing and kill sounds for host and stock guests](remote-event-audio.md)
reuses native cosmetic messages with single-event delivery and no extra local playback.
`0.54.3` removes unrelated kill-audio admission gates and adds bounded dispatch
diagnostics. Offline FMOD rendering confirms the impact event contains sound;
the reported live silence still requires host/guest reproduction.

Combat follow-up: [lobby HP/MP recovery control](lobby-recovery.md) adds an
independent toggle using shared participants and match state, preserving normal
dungeon recovery, potions and revival.

Combat follow-up: [optional PvP balancing](pvp-balancing.md) shares per-hit
softening, a shared rapid-hit damage budget and the Dark Cloud limiter across
casual friendly fire and all match modes. `0.54.2` increases damage headroom
and slows debuff damage under the same toggle while retaining Freeze's stun.
`0.54.4` shortens Shock/Freeze's separate damage intervals to 0.25 seconds.
`0.55.0` makes the shared refill configurable through the Combat panel, commands
and presets. `0.55.1` tunes the default to **70% of victim max HP/sec**.
`0.55.2` shortens Dark Cloud's separate PvP strike cooldown to **0.25 seconds**.

Compatibility fix: [co-installed Harmony and missing multiplayer FX/notices](harmony-compatibility.md)
records the host-log failure and compiled API regression check in `0.51.3`.

Combat follow-up: [Dark Cloud player strike limit](friendly-fire-artifacts.md#dark-cloud-player-strike-limit-0512-2026-10-13)
limits repeated strikes to the same hostile player while preserving native enemy behavior.

UI follow-up: [tabbed draggable window and native Settings hotkey](tabbed-settings-ui.md)
implements the approved option 2; the [panel guide](control-panel.md) covers usage.
The latest [performance review](performance-review.md) measures tab-specific
snapshot capture while preserving current readiness and synchronization checks.

Recovery follow-up: [late-run Deathmatch and Revive All availability](deathmatch.md#late-run-availability-fix-0431-2026-10-11)
distinguishes native chapter/outcome markers from actual run settlement.

| Document | Use it for |
| --- | --- |
| [General findings](general-findings.md) | SDK/loading, build and inspection workflow, shared implementation patterns, verification and performance lessons. |
| [Domain findings](domain-findings.md) | Game-specific behavior, multiplayer boundaries, state lifetimes and feature interactions that caused previous bugs. |
| [References](references.md) | External SDK, source examples, game data and tool references, with their recorded provenance and limits. |
| [Module structure](module-structure.md) | Source ownership and where to extend an existing feature. |
| [Synchronization extension guide](synchronization-guide.md) | Current shared commands, reconciliation, native boundaries, write journals and recovery. |
| [Development notes](development-notes.md) | Version history, initial setup, assembly fingerprint, research history and earlier verification records. |

## Find the relevant feature

| Area | Reading |
| --- | --- |
| State lifecycle and recovery | [Lifecycle audit](sync-lifecycle-audit.md), [session inheritance](session-inheritance.md), [re-entry review](session-reentry-review.md), [penalty-stat review](penalty-stat-sync-review.md) |
| Stats and numeric modes | [Stats](stat-command.md), [visible C-menu allowlist](visible-stat-modifiers.md), [relative consistency](relative-stat-consistency.md), [multipliers](multiplier-command.md), [reset](command-reset.md) |
| Companion modifiers | [Six native owner bonuses, commands, units, inheritance and stock-guest behavior](companion-stat-modifiers.md) |
| Fountain and choices | [Fountain](fountain-command.md), [second-run carryover](fountain-run-restart.md), [bond-artifact investigation](bond-artifact-fountain-investigation.md), [choices](choice-command.md) |
| Starting resources and capacity | [Commands](resource-command.md), [native investigation](resource-settings-investigation.md), [implementation/follow-ups](resource-settings-implementation.md), [leaves departure timing](leaves-departure-fix.md) |
| Persistence and UI | [Presets](session-preset.md), [control panel](control-panel.md), [panel implementation](control-panel-implementation.md), [localization](localization.md) |
| UI revamp options | [Native Settings vs separate draggable window vs current-panel refresh](ui-revamp-investigation.md), including widget support, input/lifecycle constraints and the recommended direction |
| Names and stock guests | [Compatibility scope](presentation-compatibility.md), [gradient](name-gradient.md), [historical UI omissions](name-ui-follow-up.md) |
| Portal ping audio | [Native notification/audio coupling and host-only limitation](portal-ping-investigation.md); research only, pending a scope decision |
| Rabbit healing | [Potion options](rabbit-potions.md), [shared particles](rabbit-shared-healing-visuals.md), [healing-book feasibility](healing-item-investigation.md), [level-up potions](rabbit-level-up-potions.md) |
| Costume-owned effects and items | [Rabbit starting artifacts: Iron Wall and Blood of Obrus](rabbit-starting-artifact.md), [Collin grants](collin-starting-artifact.md), [Bat HP steal](wingless-bat.md), [given-item restrictions](item-restrictions.md) |
| Encounters and spawning | [Merchant variants/current rules](merchant-variants.md), [initial Wandering Merchant design](wandering-merchant.md), [Mystic Jar](mystic-jar.md), [random event rooms](random-events.md) |
| Combat | [Friendly fire, KDA, companions, reflection and revive-all recovery](friendly-fire.md), [offensive artifact targeting audit](friendly-fire-artifacts.md), [Bolt and shared spell targeting](friendly-fire-spells.md), [downed-player visual and native FX research](friendly-fire-death-visuals.md) |
| Combat participants | [Temporary selections, Select All/reset, connection lifetimes and shared deathmatch eligibility](selective-friendly-fire.md) |
| Private kill notices | [Victim-only chat, matchup totals, score/session resets and stock-client transport](friendly-fire-killed-by.md) |
| Deathmatch | [Temporary timed matches, respawns, rankings and toggle synchronization](deathmatch.md) |
| Automatic respawn FX | [Shared Deathmatch/Tournament success boundary and native white pillar](respawn-visuals.md) |
| Tournament | [Shared match runtime, one-on-one elimination, bounded draws, participant lifetimes and controls](tournament.md) |
| Play Tag | [Bomb rules, commands/UI, finite turns, shared lifecycle and native death/explosion evidence](play-tag.md) |
| Reliability and performance | [Disconnect investigation](disconnect-investigation.md), [penalty warnings/transport evidence](penalty-stat-sync-review.md), [performance reviews](performance-review.md) |
| Releases and dependencies | [GitHub updater](github-updates.md), [updater investigation](github-release-updates-investigation.md), [third-party notices](third-party-notices.md) |

## How to interpret the material

- Feature documents often preserve the original design followed by dated fixes.
  Read the latest follow-up before applying an older rule. For example, the
  original resource plan froze starting-leaves intent; the later departure fix
  deliberately reads current intent for the outstanding grant.
- [Synchronization architecture](synchronization-architecture.md) is a historical
  design. Its local presentation registry was removed in `0.12.2`. Use the
  extension guide and compatibility scope for current behavior.
- `plans/` and `superpowers/plans/` preserve task plans and completion records.
  They are research history, not instructions to execute old tasks or proof that
  every proposed behavior shipped.
- Source, tests and the matching installed-game contracts resolve ambiguity.
  Record a discrepancy when a document is stale instead of copying it into a new
  feature. Neither a fixture nor local readback proves delivery/rendering on a
  live guest. Feature documents identify remaining manual checks.

When adding findings, include the addon/game version or assembly hash, the native
symbols inspected, the observed behavior, the implementation implication, and
what remains unverified. Put detailed evidence with its feature and update this
index; keep the general/domain summaries focused on reusable lessons.
