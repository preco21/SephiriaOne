# Development knowledge index

Maintained: 2026-10-18 (Asia/Seoul), addon `0.62.1`.

PvP tuning: [Dark Cloud](pvp-balancing.md#dark-cloud) now admits one strike per
attacker/opponent every 0.5 seconds while balancing is enabled. Other effect
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
Plasma Burn, Frostbite and Poison separate 0.75-second defaults, live controls/
commands and v28 saved settings. Native PvE, Wound and separate Shock/Freeze/
Dark Cloud limits are preserved.

Combat balancing: [guard MP scaling](pvp-balancing.md#guard-mp-cost-0580-2026-10-16)
defaults to 10% maximum MP before native Guard Efficiency/perfect reductions.
Live command and dedicated balancing controls share settings across all modes;
native PvE, parries and MP shields are unchanged. Nondefault saves use v27.

Combat balancing: [HP/MP Harvest](pvp-balancing.md#harvest-recovery-0572-2026-10-16)
recovers 20% of native amounts on balanced PvP hits across all modes. Native
stats, PvE, potions and fractional MP accumulation remain unchanged.

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
