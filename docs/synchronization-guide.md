# Shared synchronization in 0.12.2

Implemented 2026-09-24 against the locally inspected Sephiria 1.0.33 assemblies.
The [architecture](synchronization-architecture.md) records the original design;
this document describes the current extension points and operational limits.
The `0.12.2` scope retains effects that reach unmodified guests through native
multiplayer state and removes the former local-only presentation layer.

## Shared paths

| Path | Common infrastructure | Feature responsibility |
| --- | --- | --- |
| `/stats`, `/choices`, `/fountain` | Ready-player collection, identity snapshots, preflight, journaled native writes, readback, fault containment, policy commit | Parsing and pure arithmetic |
| Joining/replaced avatars and saved presets | Object-lifetime enrollment, once-only outcomes, all-family planning and write batch | Compose retained settings using existing planners |
| Relative stats | Change-driven coordinator, exact observations, readiness, suspension/fault reporting | Maintain displayed native value plus cumulative offset |
| Fountain capacity and carryover cap | Player observation, ordered dependency, session invalidation, verified cap-only writes | Preserve native baseline and independent cap ownership |
| Native Fountain grant and new candidate generation | `SessionSettings.BeforeNativeRead` / `EnsureFresh` | Native consumer and existing generation guards |
| Owned name publication | Shared coordinator and readiness; bounded native-command acknowledgment retry | Format name and use the owned native transport |

Native equipment, costume, preset, passive-menu, buff, and spent hard-mode point
changes are observed through their resulting raw/bonus/amplifier/capacity values.
They do not each require another feature-specific event handler. SDK run starts
invalidate the cap even when avatar objects survive; one-time adjustments are
not replayed. Replacing an avatar with a reused network ID creates a new lifetime.

Commands retain their existing meaning: relative stats accumulate from each
character's native baseline; an increment after an absolute set starts relative
mode. Choices remain raw additive contributions affected by native multipliers.
Absolute stats and Fountain points remain one-time changes for existing avatars.
Changing retained intent does not reapply unrelated settings.

## Adding a gameplay feature

1. Verify that the native write/replication/consumer path affects unmodified guests
   when the host installs the addon. Keep parsing and arithmetic in
   `Features/<Feature>/`. Plan without writing game
   objects. Decide whether the operation is once-only, a maintained invariant, or
   a derived value; this decision prevents accidental replay of a delta.
2. Use `SessionSettings.PrepareCommand` and `HostCommandContext.CreateBatch` for a
   host command. Snapshot every input used by the planner. Add writes through
   `NativeStateWrites` or `StateWriteBatch.Add`; include all selected players in
   preflight. Commit retained policy only through `SessionSettings.Commit` after
   native readback succeeds.
3. Register maintenance with `ReconciliationCoordinator<T>`. Declare typed
   `SyncDomain` inputs/outputs, readiness, an immutable observation with exact
   equality, and an apply function returning a `ReconcileResult`. Dependencies
   sort at registration; cycles and duplicate IDs fail immediately.
4. Scope subjects by object lifetime, and call `Forget` on departure and `Clear`
   at teardown. Keep one-time inheritance separate from continuing maintenance.
   `AcceptObservation` is only for separately validated command/recovery work,
   never a way to hide an unverified result.
5. Invalidate a domain on a known native cache reset. Keep bounded observation
   of active relevant inputs to catch missed events. A critical native consumer
   uses `BeforeNativeRead`; it must not assume LateUpdate has already run.
6. Extend policy serialization only if this feature needs explicit persistence.
   Add lifecycle and fault regressions using the portable core and runtime fixture
   projects, plus installed-game signature checks for new native boundaries.

The coordinator is independent of Unity. Existing tests register synthetic
dependent rules without adding feature branches to its core.
`OnChange` rules capture their post-write observation, coalesce invalidations,
and bound reentrant processing to four passes. Nested flushes report unavailable.
`Once` rules do not silently retry a rejected enrollment after a native edit.
Waiting rules resume when ready; suspended change-driven rules resume on changed
inputs. A partial native write is contained separately and is never replayed by
ordinary frame polling.

## Native UI boundary

The addon no longer patches local name/stat labels or refreshes their panels.
Each peer renders native replicated values using the base game's settings and
refresh lifecycle. Lobby platform names have a separate source and remain native.
Do not replay a panel's `OnOpened` or an interaction method to force refresh: it
can regenerate an offer, alter selections or grant an item. See the
[host-only compatibility inventory](presentation-compatibility.md).

## Diagnostics and recovery

`/mod status` is read-only: it does not process pending joins or write game state.
It shows session epoch, run generation, intent revision, rule outcomes and
reasons, relative suspension, critical-hook availability, last boundary results,
native name synchronization status, and any before/target/readback journal.

Preflight rejects stale plans before writes. Unity/Mirror writes are not an atomic
transaction. If application throws or readback fails after a possible write,
automatic maintenance and ordinary commands pause. Failed intent is not committed
or saved. The native consumer continues, and diagnostics report degraded freshness.

Use the reported whole-family `/stats reset`, `/choices reset`, or `/fountain
reset` to recover a partial operation. Ordinary selective resets still work in a
healthy session. Requiring the full family during recovery prevents a reset of
another stat/category from finalizing uncommitted contributions. Inherited-policy
recovery can use any family's reset because that policy was already retained;
successful recovery completes enrollment and starts relative maintenance.

Recovery accepts only captured before/target values on the same native object
lifetimes and validates planning inputs/readback. Conflicting edits, replaced
inventories/dictionaries, or lost authority stop recovery rather than overwrite
unknown state. An unresolved conflict may require ending the hosted session.
Candidate unload cleanup retains its journal on failure and leaves controllers
and generation guards available for recovery; retry never subtracts twice.

## Verification and remaining limits

Portable/runtime tests cover ordinary commands, distinct baselines, presets,
restarts, delayed readiness, reused IDs, missed/duplicate/reentrant changes,
native menu/buff edits, rejection/suspension, cap ownership, partial commands,
maintenance and cleanup faults, stale object recovery, and same-frame native
reads. Name tests cover gradient formatting, publication state and bounded retries.

No custom peer protocol was added. Gameplay continues through native server
SyncVar/SyncDictionary paths; name publication uses the native owned command.
Local readback is not acknowledgment from another player's renderer. Unmodified
guests use their native UI; no presentation adapters are installed. Cached
Fountain selections and existing offers require normal native interaction; they are never regenerated
by reconciliation. Unknown game changes still require an adapter/compatibility
audit. Another addon changing raw values while retaining our markers can remain
ambiguous; no framework can infer that missing intent reliably.

Live host/unmodified-guest rendering and transport checks across
repeated runs remain pending. This implementation was built with
`-p:DeployMod=false`; no deployment script was run.
