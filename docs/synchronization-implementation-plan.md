# Shared synchronization implementation plan

**Goal:** Apply the approved reconciliation architecture to Fountain, stats,
choices, name publication and safe local presentation without changing command
semantics or requiring guest installation.

**Architecture:** Portable registered reconciliation rules own observation,
idempotence, outcomes and invalidation. A common host adapter and validated write
batch serve every gameplay command. Feature planners remain the source of
arithmetic; presentation bindings use the same coordinator with local readiness.

**Constraints:** Keep native Mirror authority and existing save format. Preserve
relative/absolute, rejected inheritance, reset and unload semantics. Never deploy;
all game builds use `-p:DeployMod=false`. Work in the isolated `SephiriaOne-sync`
checkout; integrate into main and push after review and verification.

## Execution ledger

- [x] Portable coordinator and regression tests: registered typed dependencies,
  exact observations, one-time versus change-driven rules, outcomes, bounded
  reentrant invalidations, identity cleanup and extension without core edits.
- [x] Host integration and regression tests: shared readiness/player snapshots,
  preflight/readback write batches for Fountain, stats and choices, lifecycle
  enrollment by object lifetime, relative maintenance and session cap rule.
- [x] Name and presentation integration: owned native transport plus registered
  local labels, lobby/member/status refresh, safe stat refresh and explicit cache
  coverage. Never replay native open/interaction actions.
- [x] Diagnostics, compatibility inventory, parity and transition tests; document
  unsupported native peer UI paths, live-test limits and implementation status.
- [x] Debug/Release build, portable/runtime tests, installed assembly inspection,
  independent code review.

Integration follows the verified checks: a focused Conventional Commit is
fast-forwarded into `main` and pushed using the repository's authorized workflow.
The resulting commit and remote status are recorded in Git and the task result.

## Shared interfaces

Core types live under `Synchronization/Core/`, retaining namespace `SephiriaOne`:

```csharp
enum SyncDomain { None, Identity, Stats, Fountain, Choices, Limits, Names, Presentation, All }
enum ReconcileMode { Once, OnChange }
enum ReconcileState { WaitingForReadiness, Applied, Suspended, Rejected, Faulted }
// Result: State + Detail, factory methods Applied/Waiting/Suspended/Rejected/Faulted.
// Rule<T>: Id, Inputs, Outputs, Mode, IsReady, Observe, Apply delegates.
// Coordinator<T> (T : class): Register(rule), Reconcile(subject),
// Invalidate(subject, domains), Forget(subject), Clear(), Describe(subject).
```

Observations implement exact value equality and are immutable snapshots. A
successful apply captures its resulting observation; a repeat cannot stack the
same bonus. Once rules do not silently reenroll rejected subjects. Readiness
waiting retries; suspended change-driven rules retry on changed inputs. Faults
are visible, never reported as successful writes. Invalidation raised during a
pass must survive it; nested flushes report unavailable rather than false success.

The host adapter returns a deduplicated, fully ready player batch and validates
captured object identity/readiness/state before writes. A write batch records
before/target/readback data, writes only differences, and commits retained policy
only after verified success. A partial failure remains visible until recovery;
it must not replay the original relative command automatically.

## Verification sequence

1. Add regressions at existing runtime entry points for avatar-ID reuse, common
   command preparation, native-input changes and partial failures; observe failures.
2. Add pure coordinator tests covering duplicate/missing events, dependency order,
   reentrancy, readiness, observation changes, rejection and a test-only extension.
3. Implement and migrate incrementally, running the relevant suite after each step.
4. Run full commands in the working checkout:

```powershell
dotnet build .\SephiriaOne.slnx -c Debug --nologo -p:DeployMod=false
dotnet build .\SephiriaOne.slnx -c Release --nologo -p:DeployMod=false
dotnet run --project .\tests\SephiriaOne.Tests -c Release -- 'C:\Program Files (x86)\Steam\steamapps\common\Sephiria\Sephiria_Data\Managed' '.\SephiriaOne\bin\Release\netstandard2.1\SephiriaOne.dll'
dotnet run --project .\tests\SephiriaOne.RuntimeTests -c Release
git diff --check
```

5. Review the complete change against the approved architecture. Resolve findings,
   rerun affected checks, record exact verification and live limitations, and
   confirm installed addon hashes unchanged before integration.

## Verification results (2026-09-24)

- Debug and Release `0.12.0` builds: zero warnings/errors, deployment disabled.
- 673 portable and 221 runtime fixture checks passed (894 total).
- Installed-game inspection passed: nine lifecycle paths, Fountain read boundary,
  seven presentation hook contracts, Steam identity, stat-only refresh IL, both
  candidate guard transformations, and embedded Harmony/license.
- Independent core/recovery and presentation reviews approved their final scopes.
  Regressions cover reused IDs, reentrant writes, interrupted traversal, partial
  inheritance/maintenance/cap/cleanup, replaced inventory recovery, selective
  recovery rejection, stale fault outcomes, peer markup and host-summary tint.
- Installed DLL SHA-256 remains
  `34C1A73E7F97670833CC71E2A9967096F267030DB49D9E1C8FD0FFC5C3ED4EEF`;
  metadata remains
  `DC05E1DD791F4EE071EB78ED03B839455D825A0A08D4939313CB9B6096BD7B72`.
- No deployment or live Unity/multiplayer test. See
  [compatibility limits](presentation-compatibility.md) and the
  [implementation guide](synchronization-guide.md).
