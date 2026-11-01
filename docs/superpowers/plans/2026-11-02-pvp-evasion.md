# Configurable PvP Evasion limits

**Goal:** Replace the fixed 50% balanced-PvP dodge cap with the shared soft/hard
curve. Approved defaults: enabled, soft 30%, hard 50%, applied to native stat-based
dodge probability before attacker anti-Evasion. Guaranteed item dodges stay native.

**Architecture:** Add `PvpEvasionSettings` using `PvpStatLimit`, attach it to the
existing friendly-fire policy/command/preset infrastructure, and consume it in
`FriendlyFireRuntime.EvasionChance`. No native stat writes, new RNG, timers,
network protocol, or additional game hooks. Limits accept 0..100 percentages with
two decimals, soft <= hard. Off restores native chance; reset restores on/30/50.

- [x] Add executable hit regression: Evasion 5000 against roll 46 must hit after
  diminishing returns (native chance approximately 47.3); confirm it fails first.
- [x] Add policy, command, preset-v60 and curve tests; implement settings and
  hit-local evaluation. Verify live off/reset, anti-Evasion, mode eligibility,
  guaranteed dodges, unchanged PvE and no warm-path allocations.
- [x] Add UI toggle, soft/hard chance inputs, Apply/Reset and EN/KO help/status.
  Preserve drafts and shared compatibility/host gating; edits must not stop matches
  or clear K/D/A or damage budgets. Test command lifecycle and UI bindings.
- [x] Run non-deploying Release build, pure/preset/localization/runtime/executable
  combat tests and installed native contracts. Review the focused diff, document
  native boundaries and live-verification limits, bump version, commit and push.

Validation commands use `-p:DeployMod=false`: `dotnet build SephiriaOne`,
`dotnet run --project tests/SephiriaOne.Tests`, `tests/SephiriaOne.RuntimeTests`,
and `tests/SephiriaOne.FriendlyFireTests`, all Release. Native tests additionally
receive installed Managed, built addon and installed HarmonyX paths. Do not deploy.

Review follow-up: policy-only UI controls use the current host/session predicate,
not stat-write readiness. The loading/fault UI contract failed before correction
and passed afterward. All requested checks passed; live Unity/guest testing remains
outside this non-deploying task.
