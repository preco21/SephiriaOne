# Temporary deathmatch implementation plan

**Goal:** Host command and UI start a temporary timed deathmatch for stock guests.

**Design:** One scoped state machine (idle, countdown, active) shares native chat,
KDA and validated revival. Per-player coroutines would scatter cancellation;
custom guest RPCs would require guest installation. Reuse a host tick, scoped
identity/death records and the existing stock RPCs instead. No native writes or
roster scans while idle. Match duration uses host unscaled time, defaults to 300
seconds (range 10..3600), is captured at start and may be saved in presets. Match
phase, deadlines, scoreboards and pending respawns are never saved.

Warmup broadcasts 3, 2, 1 with friendly fire temporarily off, then resets KDA and
enables friendly fire. Every current player death during the match gets a native
overhead chat countdown and full-HP revival after 3 seconds. All-dead game-over
settlement is suppressed only for recoverable deaths in this exact match scope.
Participants joining/rejoining enroll with their current avatar; account score
identity is retained, old avatar timers are discarded. Normal safe areas remain
safe. Match end captures the top five (kills descending, deaths ascending,
assists descending, stable enrollment order), disables friendly fire, immediately
revives pending players and broadcasts results. Manual on/off/reset cancels even
when selecting the current value; the requested toggle then takes effect.
Damage-scale edits continue the match. Run/session replacement cancels without
reviving old avatars. Both interfaces use the same authoritative services.

User confirmed: immediately revive pending players when ending/stopping. No game
deployment. Conventional commit and push after verification are authorized.

## Tasks

- [x] Add failing shared-command regression, then scoped runtime and command parser.
- [x] Extract shared single-player revival from revive-all; retain callback guards.
- [x] Integrate effective friendly-fire state, death/revive callbacks and scope cleanup.
- [x] Add configurable duration/preset version, Deathmatch tab and EN/KO messages.
- [x] Verify timers, expiry ordering, toggles, ranking, joins/rejoins, failures,
  late callbacks, presets, native bubble/revival contracts and inactive overhead.
- [x] Review, document findings and build Debug/Release with `-p:DeployMod=false`.

Results: 1,370 runtime checks, 261 executable combat checks, 69 Bat checks and the
complete portable/catalog/native suite pass. Both builds have zero warnings/errors.
Final review has no blocking findings. Delivery follows the authorized commit and
push workflow; no live test or deployment without separate user instruction.
