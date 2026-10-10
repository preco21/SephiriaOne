# Artifact hostility implementation plan

**Goal:** Repair host-authoritative artifact targeting under current friendly-fire
policy, including Snow Mountain Long-eared Bat, Storm Cloud and Thunder's Earring.

**Architecture:** Extend the existing explicit offensive selector registry. Keep
native selection bodies, conditions and timers; replace audited faction/combat
queries and calls. Share live policy with damage scaling. Scope generic nearest
searches to artifact callers and homing to verified artifact damage identities.
Bound synchronous artifact procs to one layer and retain exact debuff admission.

**Tech stack:** C# 8 / netstandard2.1, Harmony, native Unity/Mirror APIs; .NET 10
executable fixture and installed-game IL contract suites.

**Constraints:** Stock guests; no assets/custom RPC; no global faction or battle
mutation; off/zero and owner immunity apply to existing effects; no deployment or
game launch. All builds/tests pass `-p:DeployMod=false`. Conventional commit/push
after verification. Do not commit game assets, decompiled source or private logs.

- [x] Reproduce missing selectors/activation in executable native-shaped fixtures
  (`tests/SephiriaOne.FriendlyFireTests`); preserve no-owned-frostbite behavior.
- [x] Extend `FriendlyFireEffectHooks` and add focused artifact hooks/runtime
  under `Features/Combat`; register only inspected native consumers. Preserve
  native mask direction, range, cooldown, aim, proc prerequisites and safe areas.
- [x] Add scoped nearest-point consumers, current-ID homing with target loss on
  off/zero, and bounded native synchronous damage/debuff-spread admission.
- [x] Pin each registry/rewriter against installed IL; reject changed anchors.
  Verify off/on/zero, guests, owner, ordinary enemies, re-entry, pooled identities,
  yields, exception cleanup, nested loops, scaling and warmed allocation behavior.
- [x] Record asset-to-script mappings, audited/excluded surfaces and evidence
  limits in `docs/friendly-fire-artifacts.md`; update shared feature/index/history.
- [x] Run Debug/Release builds, combat/runtime/panel/native compatibility suites
  and read-only review. Correct the reviewed nearest-radius fixture mismatch;
  final combat suite has 603 passing checks. No deployment or game launch.

Delivery: conventional fix commit and normal push after the above verification.
Detailed findings, scope boundaries and remaining live checks are recorded in
`docs/friendly-fire-artifacts.md`.
