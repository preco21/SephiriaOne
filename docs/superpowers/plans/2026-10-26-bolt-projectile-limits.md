# Bolt Magic projectile limits implementation plan

> Execute inline with the executing-plans workflow; use one final code review.

**Goal:** Extend the existing host projectile limiter to Bolt Magic, including
staff-triggered casts, without changing accepted projectiles' native behavior.

**Architecture:** Register `ActiveSkill_Bolt.OnServerUpdate` under `bolt-magic`.
Its two spawn sites share the existing bullet reservation/rate wrapper and
lifetime ledger. Extend the shared null-result guard to pair each spawn with its
own configuration tail; never send null to `OnCreateProjectile` or homing setup.
One per-owner source counts all Bolt elements and cast origins. Exact known
damage IDs allow adopting in-flight bolts on activation. No global spawn hook,
FIFO eviction, new polling or client protocol. Existing off/64-player/12-source
defaults and independent rate settings are retained.

**Scope decision:** The user explicitly requests Bolt Magic coverage, extending
the earlier exclusion of primary magic for this family only. Both manual and
automatic/staff Bolt casts use the same source, preventing alternate casting
paths from bypassing the performance limit. Other primary magic and direct
weapons remain excluded. Early controller/casting FX are not bullet instances.

**Alternatives considered:** A staff-only gate would miss other high-volume
Bolt casters; a global magic-spawn gate would affect unrelated spells. The shared
Bolt emission boundary is the narrowest complete family boundary.

## Implementation and verification

- [x] Add failing native hook tests for both Bolt spawn sites, null callback/
  homing guards, preserved multishot progression and final boltCounter/despawn.
  Add runtime adoption/shared-source/cleanup and source command/preset checks.
- [x] Extend `ProjectileLimitHooks`, catalog and exact-ID recognition; add EN/KO
  label. Generalize guard-tail pairing without changing existing single-site
  emitters. Validate count/order/exception boundaries before installing hooks.
- [x] Run non-deploying Release build, projectile ledger/runtime/native rewrite
  suites, full installed contracts and the alternate HarmonyX API audit.
- [x] Review, document evidence/limits, bump version, commit and push. Never
  deploy or change installed game/addon files.

Validation completed for `0.96.0`: 500 native-hook checks across 63 cases,
135 runtime checks, 136 ledger checks, full installed contracts and all 41
Harmony references against the co-installed HarmonyX DLL. The new tests first
failed for absent Bolt registration/adoption, then passed. The native suite also
caught an ambiguous homing overload; binding now specifies `(bool, UnitAvatar)`.
Independent review repeated native/runtime checks and found no actionable issue.
Live Unity/stock-guest behavior and performance remain unmeasured.

## Native evidence

Assembly SHA256 `c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`.
REA evidence `ev_923fcc7469174970a8a7d14bc60f1c9d7b9c7461b76a9229c4848ef770dc3645`
confirms two `ProjectileSpawner<Bullet>.Spawn` sites at IL324/491, callbacks and
homing after each, and common boltCounter/Despawn progression after both.
`ev_cea0a70ce3d7df609d303ac7c4350197805a49d0e772746f392aa0f9f3ed9c1f`
confirms `SpecialProjectile_RandomBolt` selects an owned Bolt book and uses
`Charm_Magic.FireCasting`. Serialized assets link `Weapon_Staff_Rolling_T3Bolt`
to `Weapon_Staff_AttackRolling_Bolt(SP)` and `RandomBolt`, with the latter's
component script resolving to `SpecialProjectile_RandomBolt`.

This establishes routing, not measured 20–40 shots/second, guest FPS, or live
multiplayer correctness. Native resources/decompiled source stay outside Git.
