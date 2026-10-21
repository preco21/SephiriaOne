# Autonomous artifact PvP cadence

Approved scope: optional hard minimum hit interval, default off / 2 seconds,
per attacker, artifact identity and opponent. Applies only to balanced PvP in
the shared casual/match infrastructure. Excludes direct/special weapon attacks,
books, companions, planets and Frost Relic armaments except the explicitly
requested Guillotine. No deployment.

1. Audit installed emitter code and serialized identities. Use an explicit
   catalog, not a name/category prefix; keep both Guillotine variants together.
2. Test exact policy/commands/preset round trips and impact cadence, exclusions,
   lifecycle resets, live edits, budget interaction and allocation behavior.
3. Reuse the existing receipt/pair balance lifetime. Reject cooldown repeats
   before native impact callbacks; reserve clocks only at admitted positive
   damage. Recheck before reservation for nested callbacks. Dark Cloud composes
   the longer of this interval and its existing whole-strike control.
4. Add host commands, PvP Balance controls, EN/KO strings and preset support.
   Existing DoT controls continue owning secondary status tick behavior.
5. Validate non-deploying build, fixtures and installed-game contracts; review,
   document evidence and limits, conventional commit and push main.

No polling, entity search, native stat mutation or custom guest messages. Fixed
catalog lookup and lazy fixed-size per-pair clocks; native PvE and visual
emission remain intact. Live interval/toggle edits preserve score/match state
and the last hit time. Connection/life/selection/score/floor resets use existing
balance lifecycle.
