# Artifact sword hostility implementation plan

> Execute inline using the executing-plans workflow, followed by a fresh code review.

**Goal:** Plitvice swords and related secondary attacks respect the existing host-authoritative friendly-fire policy.

**Architecture:** Retain native spawning, cooldowns, costs, hit bookkeeping and RPCs. Adapt confirmed offensive faction vetoes to `FriendlyFireRuntime.ItemTarget`; retain the central damage handler for scaling, participant lifetimes and match restrictions. Avoid global faction changes or replacement attack loops.

**Constraints:** Host-only installation, unchanged PvE/off behavior, no deployment. Conventional commit and push after verification.

- [x] Reproduce the Plitvice collision veto and cover Flame Sword combo search in executable Harmony tests.
- [x] Add a narrowly validated inline-mask adapter for `IceSword.Update` and register `ComboEffect_FlameSword.SearchTarget` in the existing selector registry.
- [x] Verify live toggles, zero damage, selection/match changes, connection retirement, native hit cadence, single scaling, native enemy handling, and changed-IL rejection.
- [x] Audit Ember/Frost/Flame Sword item assets and downstream projectile, collision, return-sword and combo paths; record exact scope and remaining live-test limits.
- [x] Build without deployment, run combat/runtime/installed-game contracts, request review and update version/docs.

Final integration: a focused Conventional Commit and push to the configured
remote, as authorized in AGENTS.md. Deployment remains disabled.

## Evidence and decisions

- Installed assembly SHA-256: `C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
- Item 1248 (`Item_FrostMP_Name`, Droplet of Plitvice) uses `Charm_IceSword`, then the `IceSword` collision object. Its reverse faction bitmask veto prevents entry to `ApplyDamage`.
- Flame Sword auto-search has the existing reverse selector shape; use the existing registry rather than a separate targeting mechanism.
- `EMBER` and `FLAMESWORD` are distinct native categories. Audit both, plus related `FROST` artifacts. Old asset traversal restricted to Charm/Bullet classes did not cover these secondary sword objects.
- Final verification: Release build clean; 3,900 combat checks, 2,254 runtime checks plus 69 Bat checks, complete installed contracts/catalog suite and Harmony 2.9 metadata compatibility passed. Independent review found no actionable issues.
