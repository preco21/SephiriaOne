# Wingless Bat HP steal implementation plan

Goal: default-off host toggle changes only the Bat costume's native HP steal
contribution from 5 to 1, including stock guests. Preserve other sources.

Architecture: use native costume-owned StatusInstance lifetime. Wrap the status
factory call in UpdateCostumeData to set the initial value before native apply.
Track only verified Bat HPSteal/5 instances. For live toggles, the shared write
batch updates both synchronized HPSteal and the owned instance's value, so native
unequip removes the exact applied amount. Native ClearTarget/destruction releases
tracking; session teardown/unload restores surviving owned instances. No polling,
costume re-equipping, prefab changes or client-side assets. Native saved runs
reconstruct costume statuses; explicit presets persist only the toggle.

Alternative: a separate -4 raw-stat marker would need extra reconciliation at
every costume/reset boundary. Modifying the global costume definition alone
would not update already-equipped players. Keep ownership in the native status.

- [x] Add a failing runtime command test and native lifecycle fixtures.
- [x] Add Features/Bat command, runtime ownership, validated native factory hook
  and cleanup hooks. Reject unknown status contracts and arithmetic overflow.
- [x] Connect SessionBat to PrepareCommand/Commit, inheritance, teardown and
  snapshot/preset v14. Add Bat panel and EN/KO text through the shared dispatcher.
- [x] Test default/off/on/reset, other costumes and HP-steal sources, repeated
  toggles, switch/rejoin/restart, saved presets, authority, cleanup and failures.
  Verify installed-game IL for ownership, sync and stat consumption.
- [x] Document and build Debug/Release with DeployMod=false; independent review.
  Do not deploy or claim live testing.

Verification: 69 Bat lifecycle checks / 953 runtime checks, 24 portable Bat
policy/preset checks, full installed-game compatibility suite, 1276 bundled
catalog checks and 48 localization-loader checks passed. Both production builds
completed without warnings. The existing five-player performance probe retained
zero steady synchronization allocations. Independent review identified failure
and teardown interactions; fixes and regressions were re-reviewed with no
remaining blockers. Repository workflow: conventional commit and push main after
verification.
