# SephiriaOne development notes

Recorded: 2026-09-23; updated: 2026-10-09 (Asia/Seoul).

This document records the project history, current implementation, and modding
findings collected during the initial investigation. Installed-game observations
apply to the assembly fingerprint below. Reference repositories and documentation
can change independently of that installed game version.

For agent/contributor onboarding, start with the [documentation index](README.md),
[general findings](general-findings.md), [domain findings](domain-findings.md) and
[collected references](references.md). These consolidate the research below and
the later feature documents; dated historical entries remain as evidence.

## Current status

Version `0.38.0` adds local GitHub release discovery with chat/UI notices and both
`/one update install` and an Update button. Downloads and installation require
an explicit action. The updater verifies the package, stages an immutable
versioned DLL and atomically selects it through metadata for the next full
restart. Previous DLL/metadata and user settings remain available. See
[GitHub updates](github-updates.md) for controls, packaging, rollback and test limits.

Version `0.37.2` fixes missing Korean text after addon upgrades. All 507 bundled
keys already had translations, but older saved catalogs replaced the bundled
lookup table and omitted new keys. Load/reload now fills missing entries from
bundled defaults while preserving explicit custom values, invalid-entry fallback
and existing JSON files. No extra work is added to text lookup or gameplay updates.
See [localization](localization.md). No deployment or live UI test.

Version `0.37.1` adds bounded native reflection to friendly fire. Thorns, weapon
reflection and the default Venom Spore Pouch parry counter may return damage along
the exact reverse of an admitted allied hit, using the current host damage scale.
A return cannot reflect again; unrelated nested procs remain blocked. Reflection
uses the actual source rather than a companion's owner, preserves native formulas
and uses the existing replicated HP/death/chat path. No new hooks, queues, cached
player state or RPCs; no deployment or live multiplayer test.

Version `0.37.0` fixes friendly-fire HP damage by bypassing the native player's
team-protection callback only for the exact admitted player/team hit. Native
guard MP, other callbacks and NPC safe-mode/crime checks remain intact. Player-led
companions such as Collin now target other players under the same option and
damage scale, while protecting their owner. Off/reset or 0% blocks their hits
immediately and restores peaceful targeting on the next AI update. Ownership and
policy are read live; no faction mutation, custom guest state or new polling.
See [friendly-fire scope and verification](friendly-fire.md). Not deployed or
tested in live multiplayer.

Version `0.34.0` adds default-off [costume-bound Collin](collin-starting-artifact.md)
for Mole, Farmer Squirrel and Turtle, `/one collin`, shared Costumes controls and
v15 presets. Native starting-item registration/removal supports stock guests.
Settings apply at the next costume equip or fresh-run restock; saved inventory
resumes unchanged. Weak avatar/dungeon receipts preserve exact grant provenance
through native restart metadata clearing, including off/reset and transferred
grant cleanup. Native Collin retains its shared-NPC leader behavior. No frame
loop, custom follower assets, deployment or live multiplayer test was added.

Version `0.33.0` adds a default-off [Wingless Bat HP-steal reduction](wingless-bat.md).
`/one bat hp-steal on|off`, reset/status, a Bat panel and v14 presets reduce only
the costume's own bonus from 5 to 1. Native owned-status lifetime and synchronized
stats preserve other sources and remove the correct amount on costume changes.
Fresh applications read current host intent; live toggles use shared command
batches with paired stat/status readback. Fault regressions cover switching
costumes, rejoining, new sessions, and shutdown after interrupted writes.
No per-frame work, deployment or live multiplayer testing was added/performed.

Version `0.32.0` adds [Mystic Jar spawn-rate settings](mystic-jar.md): exact
percentages or multipliers of each location's native chance, `/one jars`, a Jars
panel and v13 presets. Normal random placements only are affected; hidden-room
rewards, guaranteed placements and chapter gates retain native behavior. Settings
are read on server generation and use native visibility synchronization for stock
guests, without rerolling existing jars or adding update-loop work. Panel tabs now
wrap to two rows. No deployment or live gameplay test was performed.

Version `0.31.0` adds default-off host friendly fire, `/one friendlyfire` commands,
a Combat tab with a 0–300% damage slider, confirmed-kill chat notices and v12
presets. Native server damage/death/chat paths support stock guests without
faction changes or per-player caches. Numeric guards and scoped retaliation
protection cover invalid damage and recursive on-hit effects. See
[behavior, limits and verification](friendly-fire.md). No deployment or live
multiplayer testing was performed.

Version `0.30.0` adds default-off `/one items unlock on|off`, status/reset, an Items
panel and v11 presets. It projects native starting-item restrictions and owner
bindings into synchronized metadata for stock guests, preserving originals for
reset/unload and native saves. Fountain items already allow selling; only their
owner binding changes. Existing ground drops become server-owned while unlocked,
so an original guest's disconnect cannot destroy them. Event callbacks cover
new grants, saved loads, native sales, re-entry and second-run dictionary clears;
there is no item scan per frame. Intrinsic no-drop curse tablets still require
client-side definition changes and remain restricted. See [findings and checks](item-restrictions.md).

Version `0.29.0` extends Rabbit infinite HP potions with a scoped Tension exception.
While infinite use is on, Wing-Eared Rabbit's regular/large HP potions bypass the
native boss-combat potion block. Sample, MP/buff potions and other costumes keep
native behavior. The host filters only the server's Tension result for the exact
selected item, with current ownership/readiness checks shared with drink completion.
Existing MP fees, Survival suppression, death guards and sharing remain in effect.
Local and unmodified guest input use this same native boundary. EN/KO command,
panel and enabled costume description explain the exception. No deployment or
live multiplayer test.

Version `0.28.3` tunes all hostile merchant types to base-HP multipliers
**1/2/4/5/7/8** on main dungeon stages **1–6**, retaining ×8 on later stages.
Unknown progress uses ×1. Native stage/co-op bonuses still apply once; existing
actors keep their spawn-time health. Spawn diagnostics and EN/KO panel guidance
use the same curve. Regression fixtures cover every tier with one/five players,
all three types, optional maps, delayed guarantees and overflow handling.
No deployment or live gameplay test.

Version `0.28.2` corrects [merchant floor numbering and restart handling](merchant-variants.md#restart-and-numbering-correction-0282).
Minimum-floor conditions, guarantee eligibility/cap reservations and HP factors
now use main dungeon stages rather than counting maps within a stage. Existing
targets, consumed rolls and completed guarantees survive mid-run setting changes.
A fresh save cannot initialize merchant state against the previous run's dungeon
during restart. Per-spawn diagnostics distinguish guaranteed/chance encounters and
record the stage, route position, target and HP calculation. Native HP initialization
does not duplicate bonuses; native stage/co-op scaling remains. No deployment.

Version `0.28.1` also excludes Potion of Regeneration (Sample), item `37`, from
Rabbit infinite-potion retention. A completed Sample drink consumes one unit
normally and retains its Survival bonus, including death during completion.
Neither infinite use nor Survival suppression alone admits a Sample drink into
the addon death guard. Independent MP cost/shared healing and regular HP-potion
protections remain. EN/KO command, panel and tooltip text state the exception.
No deployment or live gameplay test.

Version `0.28.0` adds an off-by-default [Rabbit level-up potion reward](rabbit-level-up-potions.md).
`/one rabbit level-up-potion on|off` and the Rabbit panel grant one random native
non-HP/MP potion per earned level. The audited pool includes 19 potions and
excludes random-stat potions that can grant MP regeneration. Shared session policy,
native host inventory replication, independent compatibility, EN/KO presentation
and v10 presets cover current and joining players. Restore paths do not replay
past rewards; overflow uses native transient temporary inventory. Nested inventory
permissions preserve the caller's scope. No deployment or live gameplay test.

Version `0.27.2` excludes Potion of Regeneration (Sample), item `37`, from the
Rabbit Survival-suppression option. Its native bonus remains active on completed
drinks, including death during an admitted drink. Infinite use, MP cost and
HP-only recipient healing retain their independent behavior. EN/KO command help,
panel guidance and costume text describe the exception; regression fixtures cover
all option combinations, nested drinks and death/wield cleanup. No deployment.

Version `0.27.1` defaults newly created localization configs to Korean (`ko`).
Existing saved language choices are preserved, and invalid-file startup fallback
remains English. No gameplay or deployment changes.

Version `0.27.0` adds [Crest of the Iron Wall as Wing-Eared Rabbit's starting
artifact](rabbit-starting-artifact.md), retaining its native Blessing item. The
official database-ready event adds the existing artifact to `HolyRabbit` only;
native costume ownership grants/removes the exact instance, restocks new runs,
and replicates inventory to unmodified guests. The Crest retains its native
Sword and Shield requirement. No polling, new guest assets, custom network
messages or preset changes were added. Registration and installed-game lifecycle
checks pass; deployment and live gameplay testing were not performed.

Version `0.26.1` completes a [performance review](performance-review.md). Merchant
placement avoids a native lookup's repeated debug logs while retaining its live
safe-distance rules and final stock ownership checks. Panel captures stop copying
their fresh per-player dictionaries a second time, reducing compact snapshot
allocations by 18.8%. Pending rich-text names cache normalization instead of
allocating each retry tick. Shared synchronization remains at zero measured
steady allocations in the five-player fixtures. Gameplay, synchronization cadence,
native reads and network writes are preserved. No deployment or live profiling.

Version `0.26.0` adds floor-based HP to all addon hostile merchant types: base HP
times the current eligible route floor number, minimum ×1, while retaining native
stage and multiplayer percentage bonuses. Scaling uses the shared route ordinal
and native synchronized base-HP setter once at spawn; optional rooms inherit
current progress and unknown routes use ×1. Natural merchants, prefabs, existing
actors, attack/defense and spawn settings are preserved. Preflight validation
rejects overflow before increased HP is written. EN/KO panel/status text and
[merchant documentation](merchant-variants.md#floor-based-health) describe the
formula. Tests cover floor progression, all variants, one/five players, no stacking,
delayed guarantees and invalid health. No deployment or live Unity test was run.

Version `0.25.0` adds a per-type guaranteed-encounter toggle to the shared merchant
commands, panel, snapshots, status and EN/KO messages.
`/one merchant <id> guarantee on|off` preserves all other fields. Off leaves chance rolls active and releases a
reserved cap slot; re-enabling preserves targets, completion, counts and consumed
rolls. Existing presets default to guarantee on; nondefault intent uses v9.
The event-driven runtime and native guest networking are unchanged. See
[merchant behavior](merchant-variants.md) and the
[implementation plan](plans/2026-09-29-merchant-guarantee-toggle.md). No deployment.

Version `0.24.0` adds [independent hostile merchant variants](merchant-variants.md):
Wandering Merchant, Papyrus and Taz. Each has its own toggle, chance, earliest
eligible floor, per-run cap and random guarantee; different types can coexist on
one floor with separate stock. A catalog, composable conditions, shared runtime
and per-type run state replace the single-type flow. Host-only native networking,
1x HP scaling and exact-actor crime exemptions remain. Old commands/saves are
preserved; typed settings use v8 presets. EN/KO panel controls are updated. Native
inspection excluded mage/villager templates without working combat components.
No deployment was run.

Version `0.23.0` randomizes the [guaranteed hostile merchant](wandering-merchant.md)
across the run's potential eligible floor positions, including the first floor.
Native stage assets supply future progression without generating floors early;
branching depths and Grassland's chosen mission order have separate handling.
The saved selection survives reloads, setting changes and re-entry. Chance extras
before and after it do not fulfill the guarantee. Late enablement uses current or
future progress; unsafe/failed selected floors carry the guarantee forward.
Native HP, ownership exemptions and host-only replication remain unchanged.
No deployment was run.

Version `0.22.2` completes a [performance follow-up](performance-review.md).
Non-Status panel pages skip unused diagnostic formatting while retaining live
values, permissions, saved settings and language updates. Five-player fixture
allocation falls from 103,958 to 27,880 bytes per refresh (about 73%). Choices
cleanup now releases its recovery journal and dungeon reference after recovery
or scope teardown; weak-reference tests cover both paths. Synchronization cadence,
write validation and network behavior are unchanged. No deployment was run.

Version `0.22.1` reduces extra Wandering Merchant health from 3× to 1× normal HP.
New spawns retain native floor/multiplayer scaling and start at full health;
existing actors, spawn settings and crime exemptions are unchanged. English and
Korean help/status/panel text now reflect normal health. No deployment was run.

Version `0.22.0` implements [Rabbit shared-healing particles](rabbit-shared-healing-visuals.md).
An actual recipient HP gain invokes the game's zero-argument green-heal RPC once,
using native guest assets and replication. Post-heal lifetime checks prevent stale
effects after death, disconnect, avatar replacement or floor/run changes. An
independent optional compatibility guard disables only particles on failure.
Existing sharing controls and gameplay settings are unchanged; no deployment.

Version `0.21.0` adds [English/Korean JSON localization](localization.md) for the
panel, help, command feedback, status and addon Rabbit text/notifications. Local
`/one language en|ko|reload|status` commands select and reload editable catalogs
under the game's user-data directory. Validated, cached lookups and atomic reload
preserve the last good state on errors. Presentation refresh leaves gameplay
intent, native language, command syntax and saved presets unchanged. Unmodified
guests receive host-localized native alerts. No deployment was run.

Version `0.20.0` adds the [extra hostile Wandering Merchant](wandering-merchant.md).
The off-by-default host toggle, 0–100% chance (default 25%), first-encounter guarantee
per new run, status, panel and saved presets share the existing settings service.
Added merchants use native combat/stock networking, 3× native scaled HP and exact
actor crime exemptions. Stable per-floor run markers prevent duplicate spawns or
rerolls across reconnects, revisits and setting changes. Natural merchants keep
their normal penalties. Live gameplay confirmation remains pending; no deployment.

The earlier [shared-healing visual investigation](rabbit-shared-healing-visuals.md)
verified that `UnitAvatar.RpcBloodFestivalHealFx()` uses the exact green `HealFx`
prefab used by HP potions, without potion/talent callbacks or drinking sounds.
Version `0.22.0` integrates that native path; live appearance checks remain pending.

Version `0.19.0` adds [low-MP feedback for Rabbit healing](rabbit-potions.md).
Insufficient-MP rejection displays current/required MP to the affected drinker:
a native timed system message locally, native floating text for unmodified guests.
Guest delivery uses a validated native RPC payload targeted to the current owner;
there is no arbitrary center-message RPC for unmodified guests. Optional feedback
failure cannot bypass rejection or alter the existing infinite-potion/death/Survival
protections. No polling, new settings, client assets or persistent connection state
were added. 134 potion scenarios and installed alert contracts pass. Live UI and
guest confirmation remain pending; no deployment was run.

Version `0.18.1` fixes [Rabbit potion death timing](rabbit-potions.md). Alive/wielded
readiness was rechecked after native callbacks, so death could abandon infinite
consumption and Survival suppression for an already-admitted drink. The exact
completion now retains those protections across death/wield cleanup while keeping
ownership, session and inventory identity checks. Late already-dead completions
with active Rabbit options are rejected before any effects or MP debit. Dead
sources do not share healing. Five reproductions were repaired; 115 Harmony
scenarios and installed death/consumption contracts pass. No deployment was run.

Recipient-Survival verification for `0.18.0`: shared Rabbit potion healing already
uses `HealPercent` without the potion-drink event, so nearby recipients of any
costume gain no Survival stats. Added regression cases for supported potions,
both costumes, toggle states, separate native drinks and failed recipient callbacks;
76 Harmony scenarios and installed-game contracts pass. No runtime hook change
was required, and no global talent suppression was added. See [Rabbit behavior](rabbit-potions.md).

Version `0.18.0` makes the Rabbit HP-potion MP fee configurable in game through
`/one rabbit mp-cost 25` and the Rabbit panel's amount field. Numeric values
0..10000 set and enable the fee; on/off retain it, and reset restores 10/off.
Custom fees persist independently of the toggle in v6 presets. Status and the
host's costume description show the current amount. Each completed drink reads
current host intent; zero does no MP write. Existing HP-only, Rabbit-only and
unmodified-guest behavior remains. See [verification and manual checks](rabbit-potions.md).

Version `0.17.0` extends [Rabbit potion options](rabbit-potions.md) with independent
MP-cost and Survival rank-5 suppression toggles. Initial MP cost is 10 at the
completed HP-potion attempt; insufficient MP rejects the drink, cancelled drinks
cost nothing. Suppression targets the random-stat passive callback, preserving
other potion events. All options stay scoped to HolyRabbit and stock HP-potion
IDs 0/1/37. Commands, panel, description and v5 presets share current host intent.
Old presets leave the new toggles off; unmodified guests retain native descriptions.
Deployment remains disabled.

Version `0.16.0` adds independent [Wing-Eared Rabbit potion options](rabbit-potions.md):
infinite healing-potion uses and nearby potion healing through native host events.
Commands, the Rabbit panel, resets and v4 saved presets share the existing policy.
Rejoining players read current intent on use; no healing is replayed by sync.
The host's costume description shows enabled options through a shared read-only
settings notification. Unmodified guests retain native description text.
Both toggles default off. Automated verification is documented in the feature
guide; live multiplayer/UI smoke testing remains pending. No deployment was run.

Investigated a [host-only healing spell book and infinite potions](healing-item-investigation.md).
A genuinely new legendary item requires matching guest definitions; host-only
reuse of an existing HP potion appears feasible by suppressing consumption after
its normal effect. Wing-Eared Rabbit natively shares buff spells, so nearby
potion healing is an explicit host-side extension, now implemented above.
The earlier investigation itself made no gameplay changes or deployment.

Version `0.15.5` handles [penalty-costume stat multipliers and native fallback](penalty-stat-sync-review.md).
An incompatible stat factor preserves that player's exact native raw value and
zero addon contribution while other players/settings still apply. The shared
coordinator recognizes verified native restoration as fresh; incomplete joins,
rejected plans and partial writes remain unready. Today’s host log connects the
old suspended stat status to the Fountain warning, but does not establish the
disconnect cause. It also contains Steam send-buffer errors and one timeout.
Deployment remains disabled.

Version `0.15.4` fixes [starting leaves changed just before departure](leaves-departure-fix.md).
The pending first-departure grant reads current host intent rather than the value
captured when the lobby character initialized. Paid seed, spending, reconnect
balances and completed grants remain intact. Command/panel hints describe the timing.

Version `0.15.3` investigates [two multiplayer disconnects](disconnect-investigation.md).
Their cause remains unconfirmed. A separate live talent-preset rejection no longer
escapes into Mirror's disconnect handler; initial save-restoration guards remain.
Event-only diagnostics record Steam close reasons and raw sync context before
teardown, with no network messages or reconciliation. Five-player departure/re-entry
and unchanged-write coverage was added. This version has not been deployed.

Version `0.15.2` reduces steady synchronization allocations with typed observations,
cached immutable keys, reusable temporary collections and cached name diagnostics.
Per-frame polling and native-read safety guards remain active. See the measured
[performance review](performance-review.md) and its fixture/live-profiling limits.

Version `0.15.1` audits [repeated full-quit/reconnect](session-reentry-review.md).
Inventory/talent checkpoints distinguish their applied command from current host
intent, honor offline resets safely, and preserve saved native gains as part of
the character baseline. Other sync paths already covered new avatar lifetimes
and remain unchanged. Pending unsafe decreases are visible in status.

Version `0.15.0` adds [five resource controls](resource-command.md), shared command,
panel, preset and synchronization support, native grant/load guards, and saved-run
capacity/budget checkpoints. `/one` replaces the generic `/mod` command namespace;
the old token is passed through for other addons. See the
[implementation/review record](resource-settings-implementation.md). Resources
use v3 presets; v1/v2 remain readable.

The reported Resources warning was traced to a HarmonyX runtime lacking
`LoadsConstant(CodeInstruction, string)`. Starting-grant transpilers now inspect
string-load opcodes directly, and compatibility groups install independently.
The [bond-artifact investigation](bond-artifact-fountain-investigation.md) found
that stock guests filter those items locally; no host-only menu toggle was added.

Version `0.14.0` adds [native-baseline multipliers](multiplier-command.md) to
stats and Fountain (`x3` or `set x3`). Each player's own native displayed value
is multiplied; factors replace prior adjustments, follow native changes and
survive explicit save/reload and joins. `x1` restores native, and a numeric delta
after a factor starts a new native-relative offset. The panel's Set button shares
the same syntax. Choices remain extra-count commands and reject multipliers.

Investigated [five additional resource/capacity settings](resource-settings-investigation.md):
initial dice, inventory slots, talent points, fruit-skewer points and initial
leaves. All have native host-to-guest paths, but need phase-aware grant/load
handling and safe reductions. They are implemented in `0.15.0` as described above.

Version `0.13.0` adds a [host settings control panel](control-panel.md), opened
from the pause menu or `/one ui`. Stats, Fountain, choices, presets and status
share validated actions and read-only snapshots with chat. The panel uses native
UI focus/cancel handling and clears stale session/run drafts. No hotkeys were
added; the first version uses mouse controls and keyboard input. Live rendering
and input verification remain pending. No deployment was performed.

Version `0.12.2` narrows the addon to effects that can reach unmodified guests
when the host installs it. Removed local name/UI adapters, platform-name styling,
and automatic stat-panel refresh. Fountain, stats, choices, and the native
multiplayer character-name gradient remain. Lobby/status platform nicknames
retain their native appearance; the addon no longer applies solo label styling.
See the [host-only compatibility inventory](presentation-compatibility.md).
No deployment was performed.

The [shared synchronization architecture](synchronization-architecture.md)
introduced in `0.12.0` remains across Fountain, stats, choices and owned name publication.
Commands share readiness, snapshots, journaled writes, readback, and fault recovery.
See the [extension guide](synchronization-guide.md) and
[compatibility inventory](presentation-compatibility.md). `/one status` now
reports revisions, rule outcomes, critical boundaries, and partial-write journals.

SephiriaOne is a C# addon using Sephiria's built-in HorayMod API. Version `0.11.1`
repairs Fountain caps after native loadout changes and reconciles ready state
immediately before item granting. The [lifecycle audit](sync-lifecycle-audit.md)
records event coverage and remaining native UI/cache limitations. Version `0.11.0`
made relative stat commands cumulative from each character's native baseline,
switched a delta after `set` back to relative mode, and maintained displayed offsets
after gear/buff changes. See the [consistency audit](relative-stat-consistency.md).
`/one status`, `/one save`, and `/one forget` inspect current adjustments
and explicitly save one preset for future hosted sessions. See
[commands and persistence behavior](session-preset.md). It also repairs the
Fountain carryover limit after returning to the lobby, so later runs
honor the increased allowance without another command. See the
[bug investigation and verification](fountain-run-restart.md). The addon also
retains successful host `/fountain`, `/choices`, and `/stats` settings for newly
joined players in the same multiplayer session. Each ready avatar inherits once;
resets also remove retained settings. See [session inheritance](session-inheritance.md).
The addon keeps its per-letter name gradient from `#408af1` to `#a8d7fa`, ten
supported character stats, extra item/anvil/miracle candidates, and Wishing
Fountain points. Gameplay changes use native server synchronization and preserve
normal upgrades when reset. This version was built and tested without deployment.

| Area | Status and evidence |
| --- | --- |
| Project | Visual Studio solution and `netstandard2.1` class library exist. |
| Compiler | .NET SDK `10.0.401` is installed and was used successfully. |
| Release build | `dotnet build SephiriaOne.slnx --configuration Release --nologo -p:DeployMod=false` passed for `0.14.0` with 0 warnings/errors. See [multiplier verification](multiplier-command.md#verification). |
| Debug build | `dotnet build SephiriaOne.slnx --configuration Debug --nologo -p:DeployMod=false` passed for `0.14.0` with 0 warnings/errors. |
| Automatic deployment | The build invokes `scripts/Deploy-Mod.ps1` after the MSBuild `Build` target. |
| Visual Studio command | Added the `Deploy Mod` launch profile. Its command was verified with evaluated Release properties and matching deployed hashes; Debug path resolution was also checked. The IDE dropdown has not been tested interactively. |
| Missing addon folder | Deployment created `AddOns\SephiriaOne` during verification. |
| Existing addon folder | Running deployment again succeeded. |
| Deployed content | `0.14.0` was not deployed. The user-installed files identify `0.11.0`. Output embeds Harmony and its license; no game DLLs are distributed. |
| In-game loading | The user confirmed `0.1.0` loaded. `Player.log` also contains `[SephiriaOne] Loaded v0.1.0`, the AddOnLoader success entry, and `[SephiriaOne] All databases ready`. |
| Name gradient | Per-letter midpoint colors from `#408af1` to `#a8d7fa` are published through the native owned character-name command in multiplayer. Native peers control rendering and refresh; lobby platform names and solo labels get no addon styling. See [current compatibility](presentation-compatibility.md). |
| Multiplayer verification | 21 portable synchronization, 11 bounded-retry and 30 gradient checks pass; installed native server setter, command transport, name getter and serialization paths pass inspection. A live second-client visual check is still pending. |
| Wishing Fountain commands | `/fountain 100`, `/fountain +10`, and `/fountain -5` update every current player's capacity through native server synchronization. The host installs the addon; guests can use the base game. See [commands, findings, and live checks](fountain-command.md). |
| Fountain verification | 44 command checks and 20 reset checks pass, including whole-batch rejection and restoration of distinct player values. Live chat interception, guest UI, and item carryover remain unverified. |
| Candidate commands | `/choices all 5`, `/choices item +2`, `/choices weapon -1`, and `/choices miracle 5` change this addon's extra-candidate contribution using synchronized native stats. See [design and commands](choice-command.md). |
| Candidate verification | 63 command checks and 8 generation-guard checks pass. Both guard transformations match the installed game methods. Unity patch installation, live peer behavior, and expanded panel navigation still require game testing. |
| Reset commands | `/choices reset`, `/choices item reset` (also `weapon`/`miracle`), and `/fountain reset` remove tracked addon adjustments while preserving native bonuses. See [reset semantics and checks](command-reset.md). |
| Character stats | `/stats luck +10`, `/stats luck -5`, `/stats luck set 100`, `/stats luck reset`, and `/stats reset` use native synchronized stats. `/stats list` shows supported names and display units. See [commands and findings](stat-command.md). |
| Stat verification | 117 command checks and 219 relative consistency checks cover parsing, units, exact amplified targets, batch rejection, native baselines, resets, and agreement between manual, inherited, maintained, and saved settings. Live host/guest UI and gameplay checks remain pending. |
| Multipliers | 85 new portable checks; 750 portable and 354 runtime fixture checks pass overall. Factors share policy/planning/presets and native synchronization; live guest rendering remains unverified. |
| Joining players | Successful settings apply once after native avatar/inventory initialization, including reconnects with a new avatar. Host-only commands and native synchronization support unmodified guests. |
| Automated verification | 665 portable checks and 316 runtime command/session checks pass (981 total). Covers existing gameplay/policy behavior plus shared controls, snapshots, drafts and exceptional UI cleanup. Nine native lifecycle paths, native UI stack/cancel, name transport, both candidate guards and embedded dependencies pass inspection. Fixtures do not establish live Unity/network behavior. |
| Lifecycle audit | Native preset/costume/passive/hard-mode changes, buffs, floors, restart, joins, rejection, unload and persistence were traced. Fountain cap gaps fixed; native open-panel/cached-anvil limitations remain. See [findings and live checklist](sync-lifecycle-audit.md). |
| Status and persistence | `/one status` shows intent, current values, scope/rule revisions, waiting/rejected/suspended/faulted outcomes, journals and native name synchronization status. `/one save` stores all three families and rejects unresolved partial writes; `/one forget` removes only that saved copy. Automatic loading occurs once per new hosted session. |
| Host settings panel | Pause-menu button or `/one ui`; explicit actions, current player values, separate active/saved summaries and status. Shared command services preserve native effects for unmodified guests. No new hotkeys, gamepad navigation or live-rendering claim. |
| Shared settings controls | Both chat and UI use `SettingsActions`; immutable snapshots never process pending joins or mutate gameplay. Cached saved-file reads have explicit refresh and save/forget/lifecycle invalidation. See [panel architecture and tests](control-panel-implementation.md). |

## History

Stages 1–14 occurred on 2026-09-23; candidate expansion continued on 2026-09-24.

1. Investigated native AddOns development using the user's Xetsumei GitHub and
   Nexus Mods links. The initial workspace was
   `C:\Users\preco\repos\sephiria-one`, which was empty and not a Git repository.
2. Inspected the installed game, its managed assemblies, existing RaidRaid addon,
   and `Player.log`. Confirmed that the installed game already loads native
   addons. At this stage, .NET runtimes were available but no SDK was detected.
3. Downloaded ILSpy CLI `9.1.0.7988` into a temporary research directory and used
   the existing .NET 8 runtime to inspect selected game types. This did not
   require replacing or modifying game assemblies.
4. Found the official API URL embedded in `HorayModAPI` and read the developer
   reference. Compared its documented interface with the installed assembly.
5. Expanded the research to MiraItemMod, CustomCostumeAddOn,
   SephiriaChatTranslatorAddOn, ArcaneForged, and Mira's Item Mod blog post supplied
   by the user. Also inspected MiraModBase to understand dependency loading.
6. Provided Visual Studio 2026 setup instructions for a minimal logging addon.
   The user created the project and directed subsequent work to
   `C:\Users\preco\repos\SephiriaOne`. This is the active repository; it is a
   different directory from the initial hyphenated workspace.
7. Inspected the user's project, preserving its existing source and configuration
   changes. Confirmed that SDK `10.0.401` was now installed.
8. Added the deployment script and post-build target. Built Release, created the
   addon directory, deployed the DLL and metadata, repeated deployment into the
   existing directory, and compared source/destination hashes.
9. Renamed the local branch from `master` to `main` at the user's request.
10. Recorded the user's automatic commit-and-push workflow in
    [AGENTS.md](../AGENTS.md), using Conventional Commits for completed work.
11. Added a `Deploy Mod` launch profile so deployment can be run from Visual
    Studio's Start dropdown using the selected Debug or Release output.
12. Confirmed the `0.1.0` load and database-ready messages in `Player.log`, matching
    the user's successful in-game check. Inspected the name-label UI types and
    implemented blue local character/stats and existing overhead names for `0.2.0`.
13. Expanded the feature at the user's request to reach other multiplayer clients.
    Inspected the native name command, serialization, and shipped text-label assets.
    Added multiplayer-only color formatting, plain-name restoration, and portable
    synchronization checks for `0.3.0`.
14. Inspected Wishing Fountain capacity, server-side item granting, synchronized
    session limits, and local chat submission. Added host-only set/add/subtract
    commands for every current player in `0.4.0`, with native synchronization,
    validation before writes, 40 portable checks, and a reviewed Release build.
15. Used SephiriaChoiceExpander's description as a behavior reference. Located
    the three native extra-choice stats, their synchronization, and cached offer
    behavior. Added `/choices`, contribution tracking, and generation guards for
    exhausted candidate pools. Embedded pinned Harmony and its license, keeping
    the existing two-file deployment layout for `0.5.0`.
16. Added explicit reset commands in `0.6.0`. Tracked Fountain adjustments and
    cap changes, preserving later native stat changes and independent cap
    replacements. Extended candidate resets to restore native stats outside the
    expansion limit and remain available if patch initialization fails.
17. Added `/stats` in `0.7.0` after inspecting native stat arithmetic and the
    character-panel formatter. Supported ten numeric stats using displayed units,
    host-only changes for every current player, exact multiplier-aware planning,
    and tracked resets that preserve independent additive stat changes.
18. Replaced solid blue with a per-letter name gradient in `0.8.0`, following
    the requested endpoints and per-letter midpoint sampling. Reused native
    multiplayer name transport and added local text caching/restoration, legacy
    formatting normalization, Unicode handling, and portable regression checks.
19. Added session inheritance in `0.9.0`. Inspected native player initialization,
    shared readiness checks across commands, and retained successful host settings
    for new avatars. Added relative/absolute policy composition, reset removal,
    arrival-frame handling, and once-per-avatar tracking. All 391 automated checks
    and both builds passed. At the user's request, disabled deployment for these
    builds and verified that installed addon files were unchanged.
20. Investigated missing Fountain items on second/later runs. The host log showed
    native capacity rejection for four players. Traced it to `LoadDungeon`
    resetting the carryover limit while player adjustments survived. Added a
    failing regression, then used the official server session-start event to
    restore only the cap after inventory initialization in `0.9.1`. All 424
    checks and both builds passed; deployment remained disabled.
21. Added status and an explicit saved preset in `0.10.0`. Preserved set/relative
    intent across launches, used atomic replacement in Unity's user-data folder,
    and loaded before the first ready avatar. Added host-only inspection/save/
    forget commands, read-only status, malformed-file validation, and isolated
    storage/runtime tests. All 498 checks passed without deployment.
22. Audited relative stats in `0.11.0`. Reproduced multiplier-dependent differences
    between current and joining players, and raw baseline drift after canceling
    offsets under fractional multipliers. Shared baseline planning across commands,
    inheritance, presets, and automatic maintenance; implemented the confirmed
    cumulative and set-to-relative semantics. Added suspension/recovery for
    incompatible multipliers and status feedback. All 757 checks and both builds
    passed; independent review found no actionable issues. No deployment occurred.
23. Audited all synchronized features in `0.11.1` against native write paths and
    transitions. Fixed Fountain cap drift after native capacity changes, reconciled
    state before item granting, isolated negative capacities and rejected joins,
    and documented native stat-panel/anvil caches. Independent review caught the
    rejected-player enrollment issue; regression and re-review passed after repair.
    All 792 checks and both builds passed without deployment.
24. Grouped 27 production source files by feature, session/presets, chat and shared
    infrastructure, and mirrored those groups for 11 portable test files. Updated
    linked-source paths and docs while preserving namespaces and every moved
    source file's contents. All 792 checks and both builds passed; version remains
    `0.11.1`, with no deployment. See [module structure](module-structure.md).
25. Audited overlooked multiplayer name surfaces and designed shared reconciliation
    before runtime changes. With approval, implemented `0.12.0`: common host
    snapshots/write journals, object-lifetime enrollment, relative-stat/cap rules,
    critical Fountain/candidate reads, owned-name retries and registered local UI.
    Independent review found recovery/enrollment/cleanup and TMP restoration edge
    cases; regressions and fixes now cover them. All 894 checks and both builds
    passed; installed-game contracts passed inspection. No deployment or live
    multiplayer/rendering test was performed.
26. Continued name UI coverage in `0.12.1`. Fixed the reflected Steam ownership
    equality trap, mapped received style to platform labels by unique Steam ID,
    and registered live party labels with nickname, localization and width
    preservation. Regression checks cover identity isolation, native style
    changes, departure, rename, restoration and the actual ownership adapter.
    See [follow-up findings](name-ui-follow-up.md).
27. Narrowed scope at the user's request in `0.12.2` to effects that reach
    unmodified guests from a modded host. Removed the client-only presentation
    registry, UI hooks/adapters, platform-name styling and stat-panel refresh.
    Replaced the local name-color controller with a thin native name publisher;
    retained all gameplay synchronization and supporting commands/presets.
    Added installed native name-transport checks; 860 retained checks and both
    builds pass. Independent code review found no actionable issues. No deployment
    or live multiplayer test was performed.
28. Investigated a settings control panel against installed `UIBase`, `UIRoot`,
    UI/input management and options/pause lifecycles. Identified reusable command
    services, missing typed read models and dynamic-panel lifecycle requirements.
    Recorded native integration evidence, UI options, semantic constraints and
    verification checkpoints. No runtime changes, build or deployment occurred.
29. Implemented the recommended host panel in `0.13.0`, using fresh native
    uGUI/TMP controls and a pause-menu button plus `/one ui`. Extracted reusable
    action dispatch and immutable read-only snapshots for chat and UI; added
    separate active/saved summaries, cached preset inspection, fault-recovery
    controls and stale-draft protection. Review led to membership-based cleanup
    after native callback failures and a clear unavailable-choice message. See the
    [implementation record](control-panel-implementation.md) for verification.
    No deployment or live Unity/multiplayer test was performed.
30. Investigated five resource/capacity controls against the installed assembly.
    Distinguished initial grants from spendable balances, traced early inventory
    and talent restore hazards, verified native budget/resize transport, and
    documented local-profile/menu limits and the two-phase starting-leaf grant.
    Proposed shared resource definitions and initialization boundaries; no
    runtime code, addon build, game execution, save modification or deployment occurred.
31. Added character-specific `xN` factors in `0.14.0` for stats and Fountain.
    Shared parsing, retained multiplier mode, exact display arithmetic, v2
    presets and Fountain maintenance preserve native baselines across changes,
    joins and restarts. Reused the panel's Set action. Added identity/zero,
    incompatible-target, fault-recovery and legacy negative-baseline regressions.
    No new native hooks or deployment; see [verification](multiplier-command.md#verification).

The repository already contained commits `c793844` (Git configuration files) and
`736b305` (initial project files). The scaffold adjustments and deployment work
were uncommitted when the first version of these notes was written. Subsequent
commits are recorded in Git history. The configured remote is
`https://github.com/preco21/sephiria-one.git`.

## Environment and project layout

| Setting | Observed value |
| --- | --- |
| Active repository | `C:\Users\preco\repos\SephiriaOne` |
| IDE | Visual Studio 2026 Community, installed under `C:\Program Files\Microsoft Visual Studio\18\Community` |
| Build SDK | .NET `10.0.401` |
| Mod target framework | `netstandard2.1` |
| Mod assembly / namespace | `SephiriaOne` |
| Mod version / author | `0.14.0` / `preco21` |
| Game version reported by the confirmed load log | `1.0.33` |
| Game directory | `C:\Program Files (x86)\Steam\steamapps\common\Sephiria` |
| Game managed assemblies | `<GameDir>\Sephiria_Data\Managed` |
| Addon directory | `<GameDir>\AddOns\SephiriaOne` |
| Unity version reported by the game log | `6000.3.21f1 (c02631ffc030)` |
| Other installed addon observed | RaidRaid; its metadata reported `3.318.0` during the initial investigation. |
| Player log | `C:\Users\preco\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log` |

The inspected `Assembly-CSharp.dll` has this SHA-256 fingerprint:

```text
C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510
```

This identifies the inspected binary; it is not a game release number.

```text
SephiriaOne/
  SephiriaOne.slnx
  SephiriaOne/
    SephiriaOne.csproj
    Entry.cs
    metadata.json
    Chat/
      ModChatCommands.cs
    Controls/
    Features/
      Choices/
      Fountain/
      Names/
      Stats/
    Infrastructure/
      HarmonyRuntime.cs
    Synchronization/
      Core/
      Game/
    Session/
      SessionPolicy.cs
      SessionSettings.cs
      ...
      Presets/
        PresetCommand.cs
        PresetStore.cs
        SessionPreset.cs
        SessionPresetCommands.cs
    Properties/
      launchSettings.json
    UI/
    bin/Release/netstandard2.1/
  scripts/
    Deploy-Mod.ps1
  tests/
    SephiriaOne.Tests/
      Compatibility/
      Features/
      Session/Presets/
    SephiriaOne.RuntimeTests/
  docs/
    blue-player-name.md
    choice-command.md
    command-reset.md
    development-notes.md
    module-structure.md
    fountain-command.md
    fountain-run-restart.md
    session-inheritance.md
    session-preset.md
    third-party-notices.md
```

Source and portable tests are grouped by feature. See
[module structure](module-structure.md) for responsibilities and source-link
conventions. The `0.12.0` synchronization migration preserves namespaces and
command semantics while sharing lifecycle and mutation infrastructure.

- [Project configuration](../SephiriaOne/SephiriaOne.csproj) references the installed
  `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`, `Mirror.dll`,
  `Unity.TextMeshPro.dll`, and `UnityEngine.UI.dll`. All use
  `<Private>false</Private>`, preventing those game references from being copied
  into build output. `metadata.json` is copied with `PreserveNewest`.
  NuGet restores pinned `Lib.Harmony 2.4.2`; its .NET Standard reference facade
  supplies compile-time types, while its Mono-compatible .NET Framework runtime
  and license are embedded in the addon DLL.
- [Entry point](../SephiriaOne/Entry.cs) subscribes to `OnAllDatabasesReady` in
  `OnModLoaded()` and unsubscribes in `OnModUnloaded()`. It also owns a persistent
  controller object for name colors, chat commands, and session settings. Components
  are disabled on unload before the object is destroyed. Candidate contributions
  are removed before unpatching this addon's generation guards.
- [Name synchronization controller](../SephiriaOne/Features/Names/MultiplayerNameController.cs)
  observes the owned player and drives native publication/restoration. It does not
  patch name/stat labels or refresh their panels; see the
  [compatibility inventory](presentation-compatibility.md).
- [Multiplayer name synchronization](../SephiriaOne/Features/Names/MultiplayerNameColor.cs)
  publishes per-letter gradient tags through `PlayerAvatar.SetPlayerName` for the owned
  player while multiplayer is active. The profile name is read-only; native host
  run snapshots can contain the formatted runtime name. See the feature notes
  for restoration behavior and [portable checks](../tests/SephiriaOne.Tests/Program.cs).
- [Chat controller](../SephiriaOne/Chat/ModChatCommands.cs) consumes the local
  `/fountain`, `/choices`, `/stats`, and `/one` commands, reports feedback in the local game log, and leaves
  normal chat to the game's handler. The [runtime service](../SephiriaOne/Features/Fountain/FountainPoints.cs)
  accepts commands only on the host, validates all player balances first, and
  updates native synchronized capacity and carryover limits. The
  [parser and planner](../SephiriaOne/Features/Fountain/FountainCommand.cs) have portable tests.
- [Candidate service](../SephiriaOne/Features/Choices/ChoicePoints.cs) plans every selected category
  and player before changing native synchronized stats. Namespaced contribution
  markers preserve bonuses from other sources. [Generation guards](../SephiriaOne/Features/Choices/ChoiceSafety.cs)
  bound exhausted pools without replacing the game's rewards or networking.
- [Session settings controller](../SephiriaOne/Session/SessionSettings.cs) waits for native
  player initialization, plans all inherited families before writing, and uses
  the same native fields and markers as explicit commands. Its portable
  [policy](../SephiriaOne/Session/SessionPolicy.cs) records successful commands and clears
  retained settings on reset, server/dungeon replacement, or unload.
- [Metadata](../SephiriaOne/metadata.json) names `SephiriaOne.dll` and
  `SephiriaOne.Entry` as the assembly and entry class.
- [Visual Studio launch profile](../SephiriaOne/Properties/launchSettings.json)
  exposes `Deploy Mod` in the Start dropdown and passes the active project's
  `TargetPath` and `GameDir` to the deployment script.
- [Deployment script](../scripts/Deploy-Mod.ps1) accepts optional `BinaryPath` and
  `GameDir` parameters. Without `BinaryPath`, it checks the project's
  `bin\Release\netstandard2.1\SephiriaOne.dll`, then the equivalent Debug path.
  Discovery is relative to the script location, independent of the working directory.
  `GameDir` defaults to `C:\Program Files (x86)\Steam\steamapps\common\Sephiria`.
  It requires the binary and adjacent `metadata.json` to exist before creating
  the destination. It derives the addon folder name from the binary filename,
  creates missing directories, and overwrites those two destination files.
  It does not copy PDBs, game assemblies, dependencies, or asset directories.
  The Harmony dependency is already embedded in the mod DLL.

The .NET SDK runs the compiler. The compiled addon targets .NET Standard 2.1
because it runs inside Unity's managed runtime. These are separate settings.
MiraItemMod, ChatTranslator, and ArcaneForged use the same target framework;
CustomCostumeAddOn uses the older .NET Framework 4.8 project format.

## Building and deploying

Run commands from the active repository root. Close the game before replacing
the addon binary, then restart it to load the new build.

```powershell
dotnet build .\SephiriaOne.slnx --configuration Release
```

The build uses the project `GameDir` property and deploys to:

```text
C:\Program Files (x86)\Steam\steamapps\common\Sephiria\AddOns\SephiriaOne\
  SephiriaOne.dll
  metadata.json
```

Visual Studio builds use the same MSBuild target. Press `Ctrl+Shift+B` to build;
use Rebuild Solution if Visual Studio skips an unchanged project and deployment
needs to run again. The deployment target excludes design-time builds and applies
to both Debug and Release configurations when the build target executes.

To run deployment as a Visual Studio command:

1. Open `SephiriaOne.slnx`. If necessary, right-click the `SephiriaOne` project
   and choose **Set as Startup Project**.
2. Choose **Debug** or **Release** in the configuration dropdown, then select
   **Deploy Mod** in the dropdown beside the Start button.
3. Press **Ctrl+F5** (**Debug > Start Without Debugging**).

The profile launches Windows PowerShell and runs `scripts/Deploy-Mod.ps1`. It
passes the selected configuration's exact DLL path and the project's `GameDir`,
so an existing Release DLL does not override the selected Debug build. Visual
Studio normally builds before starting; if that setting is disabled, build first.
The existing post-build target may also deploy during that build; both invocations
copy the same files. The profile still runs when Visual Studio skips an unchanged
build. It deploys files and does not launch the game.

This solution uses an
[executable launch profile](https://github.com/dotnet/project-system/blob/main/docs/launch-profiles.md).
Visual Studio's
[`tasks.vs.json` tasks](https://learn.microsoft.com/en-us/visualstudio/ide/customize-build-and-debug-tasks-in-visual-studio)
are for Open Folder mode rather than this solution workflow.

Build without deployment:

```powershell
dotnet build .\SephiriaOne.slnx --configuration Release -p:DeployMod=false
```

Use a different installed game directory for both assembly references and deployment:

```powershell
dotnet build .\SephiriaOne.slnx --configuration Release '-p:GameDir=D:\SteamLibrary\steamapps\common\Sephiria'
```

Deploy an existing build to the default game directory without compiling again:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File .\scripts\Deploy-Mod.ps1
```

The script prefers Release whenever that DLL exists, even if Debug is newer.
It falls back to Debug when the Release DLL is absent. If neither exists, it
reports that a build or explicit path is required; it does not build automatically.
Append `-BinaryPath '<path-to-built-dll>'` to select a particular output instead.
The selected DLL must have `metadata.json` beside it.

Append `-GameDir 'D:\SteamLibrary\steamapps\common\Sephiria'` to override the
script's default. Automated builds continue to pass the exact `TargetPath` and
project `GameDir` explicitly, so they deploy the configuration just built.

The script stops on errors. A missing source file, locked destination, or lack of
write permission causes deployment to fail; an MSBuild invocation reports that
failure through its `Exec` task. Multi-file deployment is not atomic: a later copy
failure can leave an earlier file already updated.

## Loader and API findings

These findings came from inspecting the installed game assembly, with the
[official API reference](https://teamhoray.com/mod-api) as supporting documentation.

### Loader contract

- `HorayModBase`, `HorayModAPI`, `AddOnMetadata`, and `AddOnLoader` are global types
  inside `Assembly-CSharp.dll`. No separate `HorayModAPI.dll` is needed for this project.
- `AddOnLoader` scans immediate child directories under the game's `AddOns`
  directory. A DLL placed directly in the `AddOns` root is not a complete installation.
- The supported metadata fields are `modName`, `modVersion`, `modAuthor`,
  `dllFile`, and `entryClass`.
- Missing metadata has a folder-name fallback. Missing `dllFile` causes selection
  of the first top-level DLL, and entry-class discovery also has a fallback.
  Explicit metadata avoids relying on these choices.
- The entry class must be a concrete subclass of `HorayModBase`. The loader uses
  `Activator.CreateInstance`, so a public parameterless constructor is appropriate.
- The loader calls `LoadMod(Metadata)`, which sets the instance metadata and
  invokes `OnModLoaded()`.
- `HorayModBase` is not a `MonoBehaviour`. Add a separate Unity component when a
  feature needs Unity component callbacks such as `Update()`.
- The inspected loader does not implement an explicit dependency load order or
  automatically scan a `Libs` directory. MiraModBase provides a separate bootstrap
  that explicitly loads Harmony and other dependencies before its main mod.
- Renaming an addon folder inside `AddOns` does not disable discovery. Move it
  outside that directory when an isolated test requires disabling it.

### Initialization and cleanup

`GameDataLoader.Awake()` calls `AddOnLoader.LoadAll()` before initializing the game
databases. Subscribe during `OnModLoaded()` and perform database work in the
appropriate callbacks.

| API surface observed in the installed assembly | Intended use |
| --- | --- |
| `OnLoadItemDatabase`, `OnLoadWeaponDatabase`, `OnLoadCostumeDatabase` | Register or modify the respective content. |
| `OnLoadMiracleDatabase`, `OnLoadStatusDatabase`, `OnLoadKeywordDatabase`, `OnLoadPassiveDatabase`, `OnLoadPropDatabase` | Register or modify other supported database content. |
| `OnAllDatabasesReady` | Resolve relationships across initialized databases. |
| `OnLocalizationReady` | Register translated strings through `HorayModLocalizationContext`. |
| `OnStartGameServerside`, `OnStartGameClientside` | Separate server and client game-start hooks. |
| `OnStartSessionServerside`, `OnStartSessionClientside` | Session hooks with a Boolean argument named `isSavedSession` in the internal notifier. |
| `OnFloorAllocatedServerside`, `OnFloorAllocatedClientside` | Floor hooks carrying GUID, floor name, and `FloorGenerator`. |
| `OnPlayerEvade`, `OnGetDebuff` | Additional gameplay extension points. |
| `GridInventoryStartPermission`, `GridInventoryEndPermission` | Inventory permission hooks carrying inventory and player objects. |
| `RegisterNetworkPrefab`, `RegisterNetworkPrefabs`, `UnregisterNetworkPrefab` | Network prefab registration helpers. |

The listed events were confirmed in the assembly, not individually tested in-game.
`OnAllDatabasesReady` fires before some subsequent object-pool setup in
`GameDataLoader`; it does not mean every game subsystem has completed startup.

`HorayModLocalizationContext.AddText` accepts either
`(language, key, value)` or `(key, Dictionary<string, string>)`.

The inspected `ItemDatabase` provides `Register(ItemEntity)` and
`Modify(int, Action<ItemEntity>)`. Registration rejects duplicate IDs and skips
disabled items. Its load event runs after the initial vanilla database population.

`HorayModBase` exposes `OnModUnloaded()`, and `AddOnLoader` has `UnloadAll()`.
Actual teardown behavior has not been tested. The inspected
`GameDataLoader.OnDestroy()` does not itself call `AddOnLoader.UnloadAll()`.
Do not assume that returning to the title screen invokes addon cleanup or reloads
a changed DLL. Use a full game restart during development.

The official documentation is marked work in progress. One concrete discrepancy:
it lists `OnFloorAllocated` as unimplemented, while this installed assembly has
separate server/client variants. Verify signatures against the installed DLL
before implementing features based on examples.

## Decompilation and symbol inspection

This installation contains managed Unity/Mono assemblies. ILSpy successfully
reconstructed readable C# for the inspected types. Original PDB files are not
required to inspect class names, fields, signatures, or method IL. They would help
with source mapping and debugging, but decompilation does not recover the original
comments or exact source text.

The research used ILSpy CLI `9.1.0.7988` from its NuGet package, unpacked under:

```text
%TEMP%\sephiria-mod-research\ilspycmd\tools\net8.0\any\
```

Selected type exports are in `%TEMP%\sephiria-mod-research\game-types`. These are
temporary local research files, not repository dependencies. They may disappear
when temporary files are cleaned up.

To reproduce inspection with the same CLI version using an installed SDK and
.NET 8 runtime:

```powershell
dotnet tool install --global ilspycmd --version 9.1.0.7988

$managed = 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed'
ilspycmd -t HorayModAPI -r $managed "$managed\Assembly-CSharp.dll"
ilspycmd -t ItemDatabase -r $managed "$managed\Assembly-CSharp.dll"
```

If `ilspycmd` is already installed, inspect its version before changing the
installation. The GUI can also open `Assembly-CSharp.dll` directly with its sibling
managed DLLs available for resolution. See the [ILSpy project](https://github.com/icsharpcode/ILSpy)
and [CLI documentation](https://github.com/icsharpcode/ILSpy/blob/master/ICSharpCode.ILSpyCmd/README.md).

Useful starting points for further research are `ItemEntity`, `Charm_Basic` and
existing `Charm_*` implementations, `WeaponDatabase`, `WeaponAddon`, `PlayerAvatar`,
and `DungeonManager`. These are research directions, not a claim that all their
behavior has already been examined.

Compile the addon against the original installed assemblies. Keep decompiled game
source and game DLLs out of source control and release archives. Inspection of
private members does not make them publicly callable; a feature may need a public
API, reflection, or a targeted runtime patch. Unity serialized assets also require
a separate inspection workflow from managed-code decompilation.

## Reference material collected

The observations below describe the repositories as inspected on the recorded date.

| Resource | Findings and use |
| --- | --- |
| [Official HorayMod API](https://teamhoray.com/mod-api) | Developer reference for lifecycle, database events, localization, and network prefab helpers. Compare it with the installed assembly because the API is evolving. |
| [Xetsumei profile](https://github.com/Xetsumei/) | Starting point for ModMaker, QoL, enemy modification, and DungreedEnemies repositories. |
| [Sephiria-ModMaker](https://github.com/Xetsumei/Sephiria-ModMaker) | Optional content editor and runtime. Current tree primarily contains binaries, metadata, and documentation; useful for authoring workflows and supported content categories. |
| [sephiriaQoL](https://github.com/Xetsumei/sephiriaQoL) | Feature and compatibility reference. Current tree primarily distributes compiled binaries and documentation. |
| [SephiriaEnemyModify](https://github.com/Xetsumei/SephiriaEnemyModify) | Enemy-modification reference, also primarily a binary/documentation distribution in the inspected tree. |
| [DungreedEnemies](https://github.com/Xetsumei/DungreedEnemies) | Additional Xetsumei content reference. Repository layout was checked; implementation was not deeply reviewed. |
| [MiraItemMod](https://github.com/Mira090/MiraItemMod) | Main source reference for content mods. `Core.cs` shows lifecycle and Harmony setup; `Data.cs` registers content; `Registries/` provides content helpers. The repository reports an MIT license. |
| [CustomCostumeAddOn](https://github.com/Mira090/CustomCostumeAddOn) | Smaller source example of event subscriptions, costume registration, runtime sprite loading, and cleanup. |
| [SephiriaChatTranslatorAddOn](https://github.com/Mira090/SephiriaChatTranslatorAddOn) | Focused source example of Harmony, configuration, coroutines, localization, and a web request. Its remote translation service is not needed by SephiriaOne. |
| [ArcaneForged](https://github.com/Mira090/ArcaneForged) | Source example of custom weapons, registration, Harmony patches, and networking integration. |
| [MiraModBase](https://github.com/Mira090/MiraModBase) | Bootstrap implementation explaining the extra loader DLL and explicit dependency loading used by Mira's distributions. |
| [Mira's Item Mod blog post](https://note.com/mira090/n/n6069655525d8) | Author's explanation of mechanics and balance decisions. Useful context for the source, rather than an SDK tutorial. |
| [Sephiria on Nexus Mods](https://www.nexusmods.com/games/sephiria) | Distribution and user-facing installation reference. Individual mods may use different loading approaches. |
| [SephiriaChoiceExpander](https://www.nexusmods.com/sephiria/mods/19) | Describes five extra item, weapon, and miracle candidates with configurable amounts. Its BepInEx-based distribution is not used by this native addon. |
| [DiceTalentMod on Nexus](https://www.nexusmods.com/sephiria/mods/18) | Concrete native AddOns packaging and session-load/logging instructions. |

Mira's projects contain developer-specific reference paths, including Harmony
paths outside their repositories. They should be adapted to this project's
`GameDir` configuration rather than copied unchanged. A reference to a Harmony DLL
under a MelonLoader directory in an example project does not make MelonLoader a
requirement for a native HorayMod addon.

## Development decisions and constraints

- Prefer the official events and database APIs for supported changes. Add a
  narrowly scoped [Harmony patch](https://harmony.pardeike.net/v2/articles/intro.html)
  when the required behavior lacks an appropriate hook. Version `0.5.0` embeds
  Harmony `2.4.2` for two candidate-exhaustion guards; see [notices](third-party-notices.md).
- Treat dependency loading and packaging as explicit work when adding libraries.
  The current deployment script only handles the starter DLL and metadata.
- Keep content IDs and saved-state keys stable across releases. Test an existing
  save when adding persistent content, and use a backed-up save for development.
- Keep features that affect unmodified guests with the addon installed on the
  host. Verify native authority, serialization and peer consumers before adding
  one; local rendering patches alone do not meet this scope. Supporting local
  command/status/preset controls remain. Live multiplayer verification is pending.
- Do not assume an added `[SyncVar]` attribute will work in an ordinary DLL build.
  Mirror synchronization requires the appropriate generated or explicit code.
  Mira's [Charm_Kill_Luck example](https://github.com/Mira090/MiraItemMod/blob/master/MiraItemMod/Items/Charm_Kill_Luck.cs)
  includes explicit serialization methods; it is an advanced reference, not
  starter boilerplate to copy without checking the game's Mirror version.
- Clean up event subscriptions and owned Unity objects when applicable, and
  remove only this mod's Harmony patches. Session transitions still need testing.
- Code-only changes and runtime PNG loading can start without a Unity Editor
  project. Custom asset bundles introduce a separate authoring/build workflow.
- Recheck referenced signatures and patch targets after game updates. Record the
  tested game version or assembly fingerprint alongside future compatibility notes.
- Publish our own code/assets and required redistributable dependencies with their
  notices. Check each reference project's license before reusing its code or artwork.

## Next verification steps

1. Manually install the `0.14.0` Release DLL and adjacent metadata when ready;
   this task did not deploy them. Fully restart Sephiria and enter the town/lobby
   or a run; the title screen alone is insufficient for addon loading.
2. Inspect `Player.log` for these expected entries:

   ```text
   [SephiriaOne] Loaded v0.14.0
   [SephiriaOne] All databases ready
   [SephiriaOne] Chat commands bound: /fountain, /choices, /stats, /one
   [SephiriaOne] Candidate commands ready: /choices (extra choices 0..20)
   ```

3. Follow the [native-name multiplayer checks](presentation-compatibility.md#verification)
   and record the result. The confirmed `0.1.0` lifecycle does not establish that
   the new color renders correctly.
4. Test panel reopening, scene/session transitions, and coexistence with RaidRaid.
   In multiplayer, check both host and client ownership and other players' colors.
5. Follow the [Fountain live checks](fountain-command.md#verification), including
   host-only access, all-player updates, reopening the panel, carryover above 12,
   invalid-command rejection, and normal chat behavior.
6. Follow the [candidate live checks](choice-command.md#live-verification) with
   an unmodified guest. Inspect `Player.log` for Harmony compatibility errors and
   check exhausted pools, rerolls, and navigation of expanded panels.
7. Follow the [reset live checks](command-reset.md#live-checks), including
   distinct player defaults, repeated reset, per-category reset, and host-only
   multiplayer use. Fountain commands from earlier versions lack reset tracking.
8. Follow the [character-stat live checks](stat-command.md#live-verification),
   including decimal critical chance, total attack speed, distinct baselines,
   buffs/equipment changing between command and reset, and an unmodified guest.
9. Follow the [late-join checks](session-inheritance.md#live-checks), including
   absolute and relative commands, resets before joining, reconnects, arrival-frame
   commands, incompatible guest stats, and leaving/restarting the hosted session.
10. Repeat the [Fountain restart reproduction](fountain-run-restart.md#verification)
    with an unmodified guest. Verify selected items above the normal allowance
    carry over on second and third runs without another points command.
11. Follow the [saved-preset checks](session-preset.md#verification), including
    full application restart, late guest joins, reset/save distinction, local
    status display, and removing a preset without changing the current session.
12. Follow the [relative-stat consistency checks](relative-stat-consistency.md#verification):
    different player baselines, cumulative offsets, set-to-relative changes,
    multiplier equipment, late joins, suspension/recovery, and saved reload.
13. Follow the [full lifecycle checklist](sync-lifecycle-audit.md#verification-and-live-checklist),
    including native presets, costume changes, passive resets, hard-mode points,
    immediate run entry after a capacity change, and cached UI limitations.
14. Inspect `/one status` and follow the [host-only compatibility checks](presentation-compatibility.md#verification).
    Confirm lobby platform names keep their native appearance and character-name
    consumers use their native refresh timing. Use the [shared synchronization guide](synchronization-guide.md)
    for fault recovery and future features.
15. Follow the [host-panel checks](control-panel.md#live-verification-still-required)
    for both entry points, native focus/cancel, scrolling/scaling, parity with
    chat, stale drafts, session transitions, presets and unmodified guests.
16. Follow the [multiplier checks](multiplier-command.md#verification) with
    unequal character baselines, repeated factors, immediate run entry, native
    loadout changes, restarts and saved factors.
