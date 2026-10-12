# Temporary Tournament (0.47.0)

Open `/one ui` → **Tournament / 토너먼트**. Set round seconds, then use Start,
Stop or Reset K/D/A. Participant checkboxes, Select All and Reset selection use
the same list as Combat and Deathmatch. Host commands:

```text
/one tournament duration 30
/one tournament start
/one tournament status
/one tournament resetkda
/one tournament stop
/one tournament help
/one save
```

The default is 30 seconds per round; valid durations are whole seconds from 10
to 3600. Starting captures the configured duration for the entire tournament.
Later edits apply to the next tournament. Preset v20 stores a nondefault duration;
older presets keep the default. Bracket, timers, results and participants are
temporary and are never saved. Deathmatch and Tournament cannot run together.

## Pairings and results

- Capture eligible, ready player connection lifetimes at start; at least two are
  required. No selection means everyone currently eligible; otherwise enroll only
  selected players. Late joins wait for the next tournament. Reconnecting never
  restores the previous connection's bracket entry.
- Select one unordered pair. Unplayed entrants come first, followed by winless
  entrants with unresolved rounds, then previous winners. Within a tier, fewer
  completed matches (then fewer wins) take priority. Prefer an untried pair over
  an equally prioritized rematch; randomize remaining ties.
- Each fighter sees `[Tournament] Round 1 | "opponent" is your opponent! Fight in: 3...`,
  then 2 and 1. The start bubble is `[Tournament] Match started: Kill "opponent"!
  (30 secs)`. These use native avatar chat, including stock guests, in the host's
  configured language. Korean equivalents are bundled. The starting bubble shows
  the round duration; it is not a continuously refreshed clock. Status/UI shows
  remaining time.
- Since `0.50.1`, the countdown round is **1 + the lower prior-win count** of the
  two fighters. A first-match entrant or winless draw survivor keeps the pairing
  at round 1; two entrants with one win each meet in round 2; two with two wins
  each meet in round 3. Unequal progression uses the less-advanced fighter.
  A previous draw between this same unordered pair inserts `Rematch` after the
  round: `[Tournament] Round 1 | Rematch | "opponent" is your opponent! Fight in: 3...`.
  Korean uses `라운드 1 | 재대결`. Draws against other opponents do not mark a new
  pairing as a rematch. Loading or countdown deaths do not count as matches.
- Friendly fire is off during preparation/countdown; only the active pair may
  damage one another during a round. The existing scale, native defenses, safe
  areas and item prerequisites still apply. Companion ownership, artifact
  targeting, reflection and debuffs use the same additional pair restriction.
- One active-round death eliminates that entrant. Deaths outside the countdown
  can also be environmental; those do not award PvP KDA. Two deaths observed in
  the same update are unresolved. A death recorded before the deadline still
  counts if host processing is delayed; a death after expiry cannot convert a
  timed draw into a loss. Extra-life prevention is not a death.
- Timeout with no qualifying death eliminates neither player. An unordered pair
  gets at most **two completed attempts** (initial round and one rematch). After
  the second draw they cannot face one another again this tournament. Other
  legal pairings continue, including winner-stage matches.
- With no legal pairs left, turn friendly fire off, recover pending dead entrants
  and broadcast up to five accumulated K/D/A results. Sort exactly as Deathmatch:
  kills descending, deaths ascending, assists descending, enrollment order last.
  All-draw tournaments are finite: at most `n * (n - 1)` completed rounds.

Survivors retain native HP/resources; this feature does not normalize loadouts
or heal living players before rounds. Shared three-second respawns restore dead
entrants, including eliminated players; elimination still prevents further fights.
Countdown deaths recover without elimination, then receive a fresh full countdown.

## Shared architecture and lifecycle

`CombatMatchRuntime` is the former `DeathmatchRuntime`, with one common owner for
scope, native death/revival nesting, respawn records, KDA participants, bubble
transport, shutdown recovery and rankings. `CombatMatchTournament` adds pair
transitions as another partial file. `TournamentSchedule` is a pure policy over
captured entrant indices, with injected randomness and no Unity/network state.
`DeathmatchFeature` remains the shared native bubble compatibility validator.
Announcement context is derived in constant time from the scheduler's reserved
pair, win totals and completed attempts. It adds no mutable match history and
does not use KDA or total matches played. Manual score resets preserve it; new
tournaments capture a new schedule. Both fighters use the same context captured
before the first bubble send.

`FriendlyFireRuntime.CanAffectTeam` applies `AllowsPair` at every audited team
combat boundary; selection is never rewritten to represent a temporary pair.
Round boundaries invalidate hit/assist receipts without clearing KDA totals.
`ScoresEnabled` keeps the shared scoreboard alive while a countdown suppresses
friendly damage. Otherwise normal monster attacks or scale edits during a
countdown could reset the tournament's accumulated scores.

Touching the friendly-fire master toggle, changing/resetting/selecting all
participants, or pruning a selected disconnected lifetime stops the tournament.
The normal requested toggle action then takes effect. Empty-selection mode
withdraws an individually disconnected entrant instead; rejoining remains a new
lifetime. Manual score reset retains bracket results, phase and recovery timers.

Run restart, dungeon/save replacement, authority loss and actual settlement
discard old state without reviving another run's players. Floor loading retains
the captured bracket; unavailable fighters wait before countdown. An admitted
native death finishes `RpcDie` before cleanup can send `RpcRevive`. A selection
change inside revival lets the current revival finish before draining cleanup.
Unselected bystanders and late joiners receive no automatic match recovery.

No new Harmony targets, coroutines, per-player controllers or custom RPCs were
added. Roster/disconnect maintenance shares the 10 Hz scan; active pair deadlines
are checked each frame. Pair scheduling is event-only O(n²), with bounded per-run
storage. Idle and warmed active living-player ticks allocate zero in fixtures.

## Verification and limits

Announcement update verified on 2026-10-12: 41,176 scheduler checks and 1,815
runtime integration checks plus 69 Bat checks. Coverage includes rounds 1–4,
uneven progression, repeated draws, fresh pairings, withdrawal, interrupted
countdowns, KDA reset, and exact EN/KO countdown messages for both fighters.
Debug/Release builds passed with zero warnings/errors. The full portable and
installed-game contract suite also passed, including 3,282 bundled catalog checks.
Deployment remained disabled; these checks do not prove live bubble layout.

Installed native assembly remains SHA-256
`C57A0DAEAB8E8D0AF7066A344133EEC4C57D8F303FD9E25DA9410FBFC4CF1510`.
Existing native chat, guest rendering, death/revival and game-over contracts are
reused; the installed-game suite verifies new UI/command dispatch, shared pair
admission and selected-connection cancellation integration.

Focused suites cover scheduler fairness/randomness/termination, command/preset
validation, EN/KO bubbles, pair-only damage and pre-guard rejection, companion and
artifact targets, debuffs/reflection, score preservation, winners/finals, two-draw
termination, countdown interruption, simultaneous deaths, manual revive, exact
disconnect/rejoin lifetimes, native callback ordering and run/session changes.
Debug and Release builds use `-p:DeployMod=false` throughout.

Verified on 2026-10-11: 27,864 scheduler checks; 1,698 shared-runtime checks plus
69 Bat checks; 739 executable combat-hook checks; 22 panel and 50 localization
checks; 3,273 bundled catalog checks and the full portable/installed-native suite.
Both builds completed with zero warnings/errors. Five-player steady-state sync
and warmed active Tournament ticks retained zero measured allocations. Reviews
identified and fixed post-deadline death attribution and misleading mode/duration
status, with regressions. Fixture timings are not a live Unity performance claim.

No deployment or live Unity multiplayer session was performed. Verify with
unmodified guests: both translated opponent/countdown bubbles, spectator safety,
HP/death/revival ordering, reconnects, floor transitions, selection changes during
a death, a second tournament/run, and top-five results. Existing tests and native
contracts cannot prove live rendering, transport delivery or every game artifact.
