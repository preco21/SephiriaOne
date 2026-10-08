# Visible status-menu modifiers implementation plan

## Goal and design

Extend the existing `/stats` system with 17 verified status-menu values, keeping
the host-only, stock-guest behavior and shared native-baseline calculations.
Use an explicit allowlist from the installed `level2` StatsPanel components and
their native `AvatarStatsHooker` consumers. The referenced statuses web page also
lists hidden effects and is not itself the eligibility boundary. Namu's English
page could not be retrieved; installed game assets/code resolve the scope.

Add toughness, dash recovery, EXP/leaf drop,
thorns, normal/dash/special/weapon/grimoire/universal damage, HP/MP steal,
defense penetration, debuff duration/damage and crossbow reload speed. Conditional
rows count as visible: preserve their native category/weapon visibility rules.

Do not add HP regeneration, magic critical stats, potion bonuses, dash count,
weapon range or other database-only values absent from the current menu. Exclude
the unused DarkCloud/FlameGround category assets because HasCategory returns false.
Physical/elemental damage (cross-stat conversions), HP/MP maxima, movement, level-up healing and crossbow capacities are visible but
use direct fields or composite weapon/hard-mode baselines, so are explicitly
deferred rather than falsely mapped to ordinary custom-stat keys. Crossbow fire
speed already derives from the existing attack-speed modifier.

Extract `StatCatalog.cs` from `StatCommand.cs`. Centralize labels and menu IDs
alongside numeric metadata; add only optional constructor metadata so the resource
planner's private synthetic stat remains compatible. No new hooks, timer loops,
network protocol, or duplicated synchronization logic. Commands, joins/rejoins,
restarts, costume/native stat changes, reset and saved presets use the existing
stat contribution ownership and relative reconciler. v17 marks presets using
expanded stats; older presets retain existing behavior.

## Work

- [x] Write/run failing allowlist, units, parser, baseline, penalty, preset and
  runtime lifecycle tests before implementation.
- [x] Extract/extend catalog and share labels with the existing panel. Wire v17
  preset compatibility without changing current stat semantics.
- [x] Verify each added key/units against installed native menu consumer IL.
  Document asset evidence, supported stats and explicit exclusions.
- [x] Translate all new labels/units into EN/KO; test catalog completeness.
- [x] Run focused tests, native compatibility suite, Debug/Release builds and
  the existing performance probe. Optimize allocations if the larger catalog
  exposes avoidable growth; retain zero-allocation unchanged synchronization.
- [x] Independent review and fix findings before conventional commit/push main.

All dotnet commands use `-p:DeployMod=false`. No AddOns deployment. Preserve
unrelated files. Automated fixtures do not establish live multiplayer or rendered
UI correctness; document manual verification limits.

Baseline performance fixture (five players, current 10 stats): unchanged sync
0 bytes/tick; compact panel 23,224 bytes/refresh; full status 109,486 bytes/refresh.
Elapsed timings are informational due to tiered JIT and host variation.

Native arithmetic audit revised the initial 21-stat proposal: GetCustomStatUnsafe
routes the four elemental keys through GetConvertedElementalDamage, which caps
converted source damage at 20 and adds incoming converted damage. They are
visible but cannot use the ordinary independent-stat planner; defer them instead
of publishing incorrect set/xN results. The supported addition is 17 stats.

## Verification results

- Debug and Release builds: no warnings or errors; deployment disabled.
- Portable suite: passed, including 331 expanded-stat checks and 1,448 bundled
  localization catalog checks; installed-game compatibility suite passed.
- Runtime fixtures: 1,237 command/session checks, including the new stat lifecycle
  cases; 69 Bat command/native-status lifecycle checks passed.
- Localization engine: 48 passed, zero failed.
- Performance fixture: zero bytes per unchanged sync tick with five players and
  all 27 stats active. Compact snapshots: 25,433 bytes/refresh (existing 25 KiB
  budget preserved); full diagnostics: 195,602 bytes/refresh.
- Independent review: no outstanding findings after excluding converted damage.
- No deployment or live Unity/multiplayer/UI validation performed.
