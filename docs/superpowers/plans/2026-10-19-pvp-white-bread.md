# PvP White Bread implementation plan

**Goal:** Suppress White Bread's added hit-invulnerability time for active PvP
participants in every combat mode, independently of optional balancing.

**Architecture:** Adapt the single native hit-invulnerability admission check.
Read the current unique artifact and its level contribution, subtract only that
contribution for the decision, and retain native duration/timer fields. Reuse
`PvpParticipation` for mode, selection and connection lifetime. No per-frame
inventory scan, cached player state, stat writes, or custom guest messages.

**Constraints:** Preserve native base/fall/dodge/revive protection. PvP off,
spectators and non-player units retain native behavior. Build with DeployMod=false;
do not deploy. Implement inline, review, then conventional commit and push.

- [x] Add a failing damage-path regression for an equipped White Bread holder.
- [x] Add isolated runtime helper and validated ApplyDamage read adapter.
- [x] Cover equipment/level changes, existing windows, all modes, selection,
      disconnect/stop, unrelated protection, invalid inputs and hot-path allocation.
- [x] Verify installed native IL and artifact lifecycle; reject changed anchors.
- [x] Build and run combat/runtime/native suites without deployment.
- [x] Update findings/version and review the final diff.

Final verification: non-deploy Release build 0 warnings/errors; 4,987 combat
checks; 3,184 shared-runtime checks plus 69 Bat checks; portable/native and
installed Harmony API contracts passed. Fresh read-only review found no
actionable issues. Live host/guest gameplay was not tested. Commit and push
follow the repository's standing workflow; no deployment is authorized.
