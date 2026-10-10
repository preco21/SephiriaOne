# Development knowledge index

Maintained: 2026-10-11 (Asia/Seoul), addon `0.44.0`.

Agents and contributors can refer to `docs/*` for research, implementation
materials, native game findings and references. Start here before repeating an
investigation. Repository workflow and deployment rules are in [AGENTS.md](../AGENTS.md).

## Start here

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
| Fountain and choices | [Fountain](fountain-command.md), [second-run carryover](fountain-run-restart.md), [bond-artifact investigation](bond-artifact-fountain-investigation.md), [choices](choice-command.md) |
| Starting resources and capacity | [Commands](resource-command.md), [native investigation](resource-settings-investigation.md), [implementation/follow-ups](resource-settings-implementation.md), [leaves departure timing](leaves-departure-fix.md) |
| Persistence and UI | [Presets](session-preset.md), [control panel](control-panel.md), [panel implementation](control-panel-implementation.md), [localization](localization.md) |
| UI revamp options | [Native Settings vs separate draggable window vs current-panel refresh](ui-revamp-investigation.md), including widget support, input/lifecycle constraints and the recommended direction |
| Names and stock guests | [Compatibility scope](presentation-compatibility.md), [gradient](name-gradient.md), [historical UI omissions](name-ui-follow-up.md) |
| Portal ping audio | [Native notification/audio coupling and host-only limitation](portal-ping-investigation.md); research only, pending a scope decision |
| Rabbit healing | [Potion options](rabbit-potions.md), [shared particles](rabbit-shared-healing-visuals.md), [healing-book feasibility](healing-item-investigation.md), [level-up potions](rabbit-level-up-potions.md) |
| Costume-owned effects and items | [Rabbit starting artifact](rabbit-starting-artifact.md), [Collin grants](collin-starting-artifact.md), [Bat HP steal](wingless-bat.md), [given-item restrictions](item-restrictions.md) |
| Encounters and spawning | [Merchant variants/current rules](merchant-variants.md), [initial Wandering Merchant design](wandering-merchant.md), [Mystic Jar](mystic-jar.md), [random event rooms](random-events.md) |
| Combat | [Friendly fire, KDA, companions, reflection and revive-all recovery](friendly-fire.md), [offensive artifact targeting audit](friendly-fire-artifacts.md) |
| Deathmatch | [Temporary timed matches, respawns, rankings and toggle synchronization](deathmatch.md) |
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
