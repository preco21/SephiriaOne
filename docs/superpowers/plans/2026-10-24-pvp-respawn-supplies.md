# PvP respawn supplies implementation plan

> Implement inline using the shared reconciliation and native revival hooks.

Goal: participating players receive one Large Potion on PvP entry and after a
successful respawn only when they no longer own one. Keep the automatic-supply
toggle, native inventory replication and all existing participant rules.

Design: retain entry reconciliation and invalidate only the revived player's
supply cursor after the native Revive method completes successfully. Capture the
supply revision and match token before revival, then verify the exact connection,
eligibility and living state afterward. Inventory writes remain deferred to the
shared supply tick. This avoids per-frame inventory scans, callback reentry and
separate per-mode grant implementations. A failed revival, departed player,
expired match or disabled option must not schedule a replacement.

- [x] Add failing runtime regressions for consumed versus retained supplies,
  failed/nested/stale revival, selected participants and all four match modes.
- [x] Extend PvpPotionSupplies and FriendlyFireNames revival hooks; reuse native
  ownership checks (belt/grid, sub-bag and temporary overflow).
- [x] Default automatic supply on to deliver the requested start/respawn behavior;
  retain explicit opt-out, update help/localization and sparse preset coverage.
- [x] Verify non-deploying Release build, runtime/combat/native contract suites,
  relevant settings/localization checks and stable nine-player allocation checks.
- [x] Document lifecycle semantics and live-testing limits, review, then commit
  and push using the authorized repository workflow. Never deploy.

Review follow-up: a successful pending revival followed by a newer failed
revival could release stale supplies. Added failing-then-passing sequential,
nested and in-flight permission-callback regressions. Beginning a revival
acknowledges old pending work; per-receipt attempt IDs guard completion and native
inventory writes. Successful new requests still use shared invalidation. Stable
nine-player ticks retain zero allocations and perform no extra inventory scans.
