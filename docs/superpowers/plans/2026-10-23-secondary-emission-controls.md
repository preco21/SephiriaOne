# Secondary Emission Controls Implementation Plan

**Goal:** Extend safe Ice Weapon coverage and add an independent creation-rate
budget; use FIFO only if native retirement is safe and does not undermine the
performance safeguard. The user explicitly authorizes retaining reject-new when
FIFO introduces regression risk.

**Architecture:** Keep source-specific creation gates and connection-scoped
accounting. Count simultaneous objects separately from a continuous token budget
for creations. No global Instantiate/Spawn/Destroy interception, delayed replay,
or per-frame scene scans. Native guest messages and lifecycle remain unchanged.

**Tech stack:** C#, Harmony, native Unity/Mirror, executable .NET fixtures.

**Status:** Implementation and automated verification completed in `0.91.0`.
FIFO rejected under the approved safety fallback; no deployment or live guest
profiling. See [results and limitations](../../projectile-emission-controls.md).

## Constraints and design

- Host only; stock guests; PvE and PvP. Exclude ordinary/direct attacks/Minigun.
- Instance defaults remain off, 64/player and 12/source.
- Approved rate defaults: off, 64/player/second, 12/source/second; one second of
  capacity, continuously refilled on demand. Both rate limits apply together.
- Denied native attempts advance original cooldowns/loops. No delayed queue.
- FIFO requires safe network retirement, callback/child correctness and bounded
  churn. If these cannot be established, retain reject-new and document why.
- Preserve existing presets; expose rates in Projectiles UI/commands with EN/KO.
- Do not deploy; all builds use `-p:DeployMod=false`.

## Work and verification

1. Audit native retirement and missing Bullet versus RPC/local lifetimes; record
   exact safe boundaries. Resolve FIFO before mutating the destruction path.
2. Add policy, command, preset and UI tests; implement independent rate settings.
   Keep runtime accounting separate from UI/serialization.
3. Add deterministic rate ledger tests for bursts, continuous refill, denied
   attempts, live changes, clock boundaries, owner invalidation and allocations.
   Integrate before native creation; connection/run cleanup must invalidate state.
4. Extend audited Ice Weapon admission adapters and native-contract tests. Bind
   actual object lifetime where support is safe; report any unsupported routes
   rather than silently counting only host-local visuals.
5. Run relevant ledger/runtime/native hook suites, Release build, shared policy/
   localization/runtime regressions. Review networking and reentrancy boundaries.
6. Update feature findings and index, increment version for shipped behavior,
   commit conventionally and push. Live guest stutter profiling remains a separate
   validation task; fixtures do not prove FPS improvements.
