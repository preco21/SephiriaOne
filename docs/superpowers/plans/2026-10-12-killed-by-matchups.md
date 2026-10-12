# Killed-by notices and matchup records

Goal: privately identify a confirmed player killer and show the victim's
accumulated head-to-head score across casual friendly fire, Deathmatch and
Tournament, using stock guest UI.

Implementation follows the existing shared death/KDA infrastructure. Store
directed kill counts by KDA score identity, not names or avatars. Commit counts
only alongside successful KDA death accounting. Clear on KDA resets, policy
toggle resets, participant/session scope replacement and addon unload. Preserve
across lives and Tournament round transitions. A same-account reconnect within
the same scope retains historical totals like KDA, but never receives an old
connection's pending notice.

Use a private native chat RPC with null bubble avatar, preserving countdown
bubbles. Do not invent a placeholder when the killer name is unavailable; omit
that notice. Keep shared public kill logs unchanged. All notices include the
record, starting at 0–1. Hosts select the EN/KO text for stock guests.

- [x] Add failing executable tests for victim perspective, repeated and unrelated
  pairs, unknown names, self/environment deaths, stale callbacks and resets.
- [x] Add matchup counters to FriendlyFireKda and extend its existing death
  notice after successful accounting. Use current participant receipts.
- [x] Add a reusable private native chat adapter, modeled on NativePlayerAlert;
  verify serializer, receiver, routing, null bubble avatar and gameplay isolation.
- [x] Add EN/KO catalog entries and shared runtime tests for both match modes,
  round transitions, score resets, run/session changes and reconnects.
- [x] Run Debug/Release and relevant suites with DeployMod=false, review and record
  findings. Do not launch/deploy the game.

Delivery follows the standing conventional-commit and push workflow. Verification
and native/live limits are recorded in [the feature guide](../../friendly-fire-killed-by.md).
