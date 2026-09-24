# Shared synchronization architecture

Historical design: approved and implemented in `0.12.0`, 2026-09-24.
**Scope narrowed in `0.12.2`:** the user requested only features whose effects
reach unmodified guests when installed on the host. The local presentation
registry, UI hooks, label adapters and stat-panel refresh described below were
removed. Native name publication and shared gameplay reconciliation remain.
The remainder records the original design, not current UI coverage. See the
[implementation and extension guide](synchronization-guide.md) and
[presentation inventory](presentation-compatibility.md) for delivered coverage.
Originally based on `0.11.1`, the [lifecycle audit](sync-lifecycle-audit.md),
and inspection of installed Sephiria `1.0.33` name consumers and UI assets.

## Problem and recommended approach

The Fountain restart/cap fixes and the lobby name omission share a structural
problem: authoritative state, dependent values, native replication, and UI caches
have different lifetimes and refresh triggers. The current session controller
centralizes some gameplay work, but each new dependency still needs bespoke
observation, enrollment, readiness, command preparation, and UI handling.

Use a shared reconciliation coordinator with typed state dependencies, a central
game adapter, and registered presentation bindings. Events request work promptly;
observation detects changes even when no event was received; critical consumers
require fresh reconciliation before using state.

Alternatives considered:

| Approach | Trade-off |
| --- | --- |
| Event bus alone | Simplifies subscriptions, but an omitted event still leaves stale state. Ordering, idempotence, and UI caches remain each feature's problem. |
| Reapply every feature every frame | Detects some drift, but can replay one-time effects, overwrite native changes, and perform excessive network/UI writes. |
| Shared reconciliation with events and observation | Recommended. Centralizes lifecycle and scheduling while preserving each feature's application semantics. Requires explicit game and UI adapters. |

This is not automatic discovery of every semantic dependency in the game.
Adding a new UI class or native value may require one adapter registration.
Existing features should not need to learn about every new screen or event.

## Compatibility and behavior contract

- Preserve host-only gameplay commands, native Mirror replication, and support
  for unmodified guests. Local UI adapters require local addon installation.
- Name publication retains the owned client's existing authority-checked native
  command. Presentation updates run locally on clients with this addon.
- Keep command syntax, stat units, reset behavior, midpoint gradient formatting,
  preset format, and explicit save/forget semantics compatible.
- Each player's baseline is their current native state excluding the addon's
  tracked contribution, including their own equipment, passives, and buffs.
- Relative stats accumulate: `+10`, then `+5` means native +15. A relative command
  following an absolute set starts a new relative offset. Existing exact integer
  arithmetic, suspension, and native baseline restoration remain authoritative.
- Absolute stat sets and Fountain point adjustments remain one-time applications
  for existing avatars; later native changes are not continuously forced back to
  the old target. Fountain's retained set/add semantics remain unchanged.
- Choice contributions remain raw additive bonuses. Native multipliers still
  affect them. Do not automatically regenerate offers, reroll outcomes, grant
  items, or discard selections as a side effect of reconciliation.
- Same-session run restarts preserve intent and successful enrollment. New host
  scopes clear active intent and load the saved preset once, as today.
- Preserve unload behavior: restore names when possible, remove choice
  contributions and patches, retain existing stat/Fountain adjustments.
- No game files, profile names, or platform nicknames are rewritten. Builds use
  `-p:DeployMod=false`; deployment remains a separate user action.

## Components and boundaries

```text
Commands / saved preset       SDK events / native hooks / observed differences
          |                                   |
          v                                   v
    PolicyStore --------------------> SyncCoordinator
                                         |
                              snapshot -> plan -> validate
                                         |
                              authorized native writes
                                         |
                           observe outcomes / native replication
                                         |
                             local read models -> UI bindings
```

| Component | Owns | Does not own |
| --- | --- | --- |
| PolicyStore | Typed intent, command composition, revisions, preset snapshots | Unity objects, arbitrary stat writes |
| GameStateAdapter | Session/player identity, readiness, stable snapshots, shared native event subscriptions, guarded writes | Feature arithmetic, presentation policy |
| SyncCoordinator | Dirty dependencies, ordering, enrollment, retries, critical flushes, readback and diagnostics | Feature-specific calculations or knowledge of every screen |
| Feature reconcilers | Declared inputs/outputs, pure plans, update mode, restoration rules | Independent update loops or native lifecycle subscriptions |
| PresentationRegistry | Subject-to-view bindings, local refresh scheduling, pooling/rebinding, label restoration | Authoritative gameplay writes or native action replay |
| CompatibilityCatalog | Required native methods/fields, hook availability, known consumers and intentional exclusions | Automatically patching unknown methods |

Use explicit typed dependency identifiers for identity, native stat inputs,
Fountain capacity, carryover limit, choice contributions, and presentation.
Scope them to the session, player, stat key, or surface as appropriate. Avoid a
string-based broadcast of "everything changed" for every ordinary update.

Each reconciler registers an ID, authority scope, required readiness, input/output
dependencies, application mode, plan/apply/readback operations, and cleanup rule.
Reject dependency cycles at registration. Keep ordering simple and deterministic;
the initial set does not require a general-purpose reactive framework.
Readiness requirements are shared capabilities, not one universal gate: a local
name label must not wait for server inventory readiness. Host gameplay features
retain the stronger current initialization checks.

### Application modes

| Mode | Existing example | Reconciliation rule |
| --- | --- | --- |
| Apply on command/enrollment | Absolute stats, Fountain points, choice raw contributions | Apply the committed command once per eligible subject; observation alone cannot replay it. |
| Maintain invariant | Relative displayed stat offset; owned player's requested name style | Recompute from current native inputs and intent. Identical observations produce no writes. |
| Maintain derived value | Shared Fountain carryover cap | Recompute from successfully enrolled players' capacities and native cap ownership, without replaying point changes. |
| Present observed state | Labels, displayed stats, Fountain budget | Refresh local presentation from current observed data; no gameplay actions. |

## State identity, revisions, and outcomes

Track separate session epochs, run generations, intent revisions, observed input
versions, and binding generations. A run generation invalidates native caches
without granting existing avatars another copy of one-time adjustments.
Intent revisions also identify which settings changed. Editing luck cannot
reapply an unchanged absolute Fountain target or grant choices again.

Player identity includes session epoch, network ID, and avatar instance lifetime.
Track inventory replacement separately. Network ID alone is insufficient after
replacement/reuse; display names are never identity keys. Remove obsolete
subscriptions, observations, queued requests, and views on departure/teardown.

Do not use a single processed-player boolean. Per subject/feature records expose
`Pending`, `WaitingForReadiness`, `Applied`, `Suspended`, `Rejected`, or `Faulted`,
together with revision, reason, input version, and expected/observed outcome.

Preserve the current all-family newcomer preflight: invalid inheritance writes
nothing for that newcomer. Only successful application enrolls its contributions
in maintenance and shared cap calculation. An initially rejected newcomer does
not silently retry on an equipment change; an explicit successful command can
enroll the affected setting. An already enrolled relative stat may suspend and
retry when its native inputs change. These are different states.

## Processing and mutation safety

1. An SDK event, native hook, command, or observation invalidates typed inputs.
   Multiple notifications coalesce, retaining a version/reason for diagnostics.
2. Capture ready subjects on Unity's main thread. Events mark work; they do not
   immediately perform gameplay writes inside partially completed native updates.
3. Build pure plans using existing planners. Preflight every selected player for
   a command and every feature for newcomer inheritance before any batch writes.
4. Revalidate identity, authority, readiness, and captured inputs immediately
   before applying. A stale plan is deferred and recomputed, never blindly used.
5. Apply only changed values through the correct native authority path. Treat a
   command as a composed intent/target, never as a delta to replay on each event.
6. Read back authoritative results before recording successful application and
   enrollment. Record the post-write observation so addon writes cannot become
   another native bonus. Changes arriving during processing remain queued.
7. Reconcile dependent derived values, then publish local presentation changes.
   UI adapters reread after the current native update/replication batch instead
   of rendering a partial raw/bonus/amplifier notification immediately.

Validation is atomic within a planned batch; Unity and Mirror do not provide a
transaction spanning writes or clients. Do not promise atomic remote rendering.
If writes throw partway through, mark the affected operation faulted, retain its
before/target/readback record, and do not report "nobody changed" or commit a
successful policy revision. Reconcile idempotent targets where ownership can be
verified; never blindly roll back over a later native edit or repeat an additive
command. Surface unresolved partial operations for explicit recovery/reset.

Another addon overwriting raw values while leaving contribution markers intact
remains ambiguous. The coordinator must report detectable conflicts, not claim
that centralization can reconstruct every external mutation.

## Events, observation, and critical boundaries

Put session start/stop, player creation/removal/readiness, identity changes, native
stat dictionary changes, inventory replacement, and UI binding lifecycle in the
game adapter. Native presets, costumes, passive edits, and Root's Retreat spending
converge through changed raw/bonus/amplifier/capacity dependencies. An unspent
passive-point reward does not imply a stat change.

Keep inexpensive observation of active relevant inputs each update as a safety
net, including input dictionary replacement and missed callbacks. Do not scan
every Unity object each frame. UI discovery occurs at known creation/open/bind
boundaries; a bounded fallback checks registered active bindings.

Critical game reads use a centralized `EnsureFresh` boundary with a declared
dependency set. The existing Fountain grant prefix must synchronously capture and
reconcile ready player capacity plus the session cap before the native clamp.
It cannot wait for LateUpdate. Newly generated choices retain their existing
generation guards; assess the required dependency flush at those known methods.

Bound reentrant processing. Nested flushes cannot return success merely because
a flush is in progress. They must verify that the requested inputs are current
or return an explicit unavailable/faulted result. Do not drop invalidations raised
by the current pass. Missing hooks and failed critical flushes are visible as
degraded coverage; preserve native behavior, but do not claim grant correctness
when readiness or compatibility prevents establishing the invariant.

## Presentation coverage and multiplayer limits

Separate character name, platform nickname, stable player identity, and style.
A presentation binding declares which subject and fields it displays. Names stay
as the game defines them; the style decorates them without replacing one identity
with another. Registry entries cover both local and remote subjects where they
can be reliably identified. Before avatar/platform mapping exists, defer rather
than coloring an unrelated player.

Required coverage inventory from the UI audit:

| Consumer | Adapter concern |
| --- | --- |
| Character panel and overhead labels | Reuse existing formatting/restoration, preserve alpha, rebind replacement objects. |
| Party HP bars and loading placeholders | Character names versus platform nicknames; transition between placeholder and live player. |
| Steam/EOS lobby rows and room-host status | Separate platform identity sources; creation, member changes, and cached host text. |
| In-game player list and other-character panel | Refresh on observed changes while open; remove/rebind departed/replaced subjects. |
| Native stat panel | Observe raw, bonus, and amplifier together; repair stale local display without triggering native commands. |
| Fountain panel | Budget/selection consistency; use a safe refresh path only, preserving selections. If unavailable, explicitly require reopening. |
| Cached candidate offers/anvils | Declare cache generation and refresh limits. Do not regenerate offers or suppress reroll based on a mismatched new count. |
| Chat and HUD notifications | Native sanitizers remove tags and disable rich text. Ordinary name publication cannot update these renderers. Preserve sanitization; a future local renderer requires separate review. |
| Host signs, dialogue, journal, ending/credits | Cached hostName or avatar-name substitutions; explicit coverage decisions and safe native hooks. |
| Profile/rename/cloud comparison/room-title input | Intentional plain/native formatting exclusions for this migration; never persist display markup. |

Do not refresh by indiscriminately calling `OnOpened()` or re-running native
interaction methods: they may recreate inventory controls or execute gameplay
side effects. Each binding must use a narrow, verified presentation path and
restore only its own changes on unbind/unload.

Local `Applied` means local readback matches; it is not a peer acknowledgment.
Unmodified guests receive existing native synchronized state, but their own
cached UI, platform-nickname labels, and sanitizers cannot be repaired by a local
UI adapter. Diagnostics distinguish native transport support, local adapter
coverage, and peer rendering not verified. Do not infer installed peer capability
without an actual capability exchange; none is required for the initial design.

## Proposed module layout and incremental migration

Keep feature arithmetic in `Features/`. Introduce `Synchronization/Core/` for
portable scheduling/identity/outcomes, `Synchronization/Game/` for Unity/Mirror
observation and boundaries, and `Synchronization/Presentation/` for registry and
binding contracts. Feature-specific reconciler/view adapters stay with their
feature. Keep `Session/Presets/` responsible for persistence, not subscriptions.
Registration stays explicit at the composition root; no reflection plugin loader
or new external runtime dependency is needed.

1. Extract coordinator, session/player tracking, common readiness, and diagnostics.
   Migrate Fountain cap observation and the pre-grant boundary first. Preserve
   existing behavior and make previous restart regressions coordinator tests.
2. Route gameplay commands, newcomer preflight, relative-stat maintenance, and
   preset loading through the shared pipeline. Preserve each application mode.
   Remove old per-feature scheduling only when its migrated path passes parity.
3. Migrate owned name publication and existing labels. Add the presentation
   registry and safe local adapters for the lobby omissions and stale name panels.
   Keep unmodified-peer limits visible.
4. Register stat/Fountain presentation and explicit offer-cache capabilities after
   auditing their safe refresh boundaries. Record unsupported surfaces instead of
   presenting them as synchronized. Chat/log styling and cutscene rendering are
   separate extensions, not prerequisites for extracting the shared core.

Each stage is independently buildable and keeps the game usable. Do not run old
and new writers concurrently for the same state. Do not migrate all features in
one unreviewable rewrite.

## Verification and acceptance

Preserve existing portable/runtime suites and installed-game signature checks.
Add shared coordinator tests, reused for every registered reconciler:

- Duplicate, missing, reordered, and reentrant invalidations; idle passes do not
  write or send names; final ready state converges without duplicating effects.
- Two players with different baselines; set/relative/reset transitions; native
  preset, costume, passive, multiplier, buff-expiration, and inventory changes.
- Run restart with the same avatar; replacement avatar with reused ID; late join,
  reconnect, departure, authority changes, teardown/reload, saved preset loading.
- Native mutation immediately followed by Fountain grant in the same frame.
- Rejected newcomers, zero-contribution successful enrollment, negative capacity,
  suspended relative stats, partial write failures, and failed critical guards.
- UI opened before a change, opened after it, hidden/reopened, pooled/rebound,
  language-rebuilt, and destroyed while work is queued. Refresh cannot reroll,
  grant items, lose selections, write profile data, or leak another user's style.
- Received native dictionaries changing in different orders. Intermediate peer
  rendering is not promised atomic; local views must converge after delivery.
- A new test-only feature registers dependencies and participates without adding
  branches to the coordinator or editing other feature implementations.

Create a checked-in compatibility inventory classifying known native consumers
as covered, intentionally excluded, or unsupported. Assembly inspection can flag
new known-field consumers or changed hook signatures for review; it cannot prove
semantic UI coverage. Unity rendering and transport still require live tests with
host, modded guest, and unmodified guest across repeated runs.

Extend `/one status` with intent revision, per-player application outcome,
suspension/rejection/fault reason, critical-hook availability, and local UI
coverage. Logging records transitions rather than repeating the same warning
every frame. Successful native writes must not be labeled "all peers rendered".

Completion requires existing behavior parity, the shared transition tests,
Debug/Release builds with deployment disabled, compatibility inspection, and an
explicit record of live tests performed versus still pending. The architecture
reduces dependence on complete event lists; it does not eliminate compatibility
work against future game changes.
