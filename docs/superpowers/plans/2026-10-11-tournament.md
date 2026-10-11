# Tournament implementation plan

**Goal:** Add host-controlled one-on-one single-elimination Tournament using the
existing friendly-fire selection, KDA, native chat bubbles and scoped recovery.

**Confirmed rules:** One loss eliminates; draws eliminate neither; at most two
attempts per unordered pair. Prefer unbeaten players with no wins, fewest matches
first; unresolved participants precede previous winners at equal participation.
Randomize equal-priority candidates. Exhausted pairings end with shared KDA top 5.

**Architecture:** Extract the existing Deathmatch runtime as `CombatMatchRuntime`
with mutually exclusive modes. Keep scope, receipts, death callbacks, respawns,
stop/unload cleanup and standings shared. Isolate pure pairing policy in
`TournamentSchedule`; Tournament-specific transitions occupy a separate partial
runtime file. All combat targeting uses the current active pair as an additional
restriction. Separate controls/duration policy share command dispatch and presets.

**Lifecycle choices:** Capture eligible connection lifetimes at start; late joins
wait for the next tournament. Retired entrants cannot regain bracket progress by
rejoining. Explicit selection edits stop/reset the match; pruning a selected
connection also cancels Tournament. Disconnect of an unselected-mode entrant
withdraws that lifetime. Warmup deaths use normal recovery; active deaths resolve
the current round after the native death callback finishes. Both deaths in one
update produce an unresolved round. Survivors retain native HP; no new healing
or inventory normalization. Only the current pair can deal friendly damage.

**Controls:** `/one tournament start|stop|status|resetkda`, `/one tournament duration
10..3600`, default 30 seconds per round, saved by `/one save`. Timer edits apply
to the next tournament. Native host-selected EN/KO messages work for stock guests.

## Tasks

- [x] Pure scheduler and deterministic regression tests: fairness, randomness,
  winners, losses, draws, pair exhaustion, withdrawals, complete termination.
- [x] Shared runtime extraction and Tournament transitions, pair combat gate,
  score preservation across rounds and callback/lifecycle regressions.
- [x] Commands, duration policy/presets, compact Tournament tab and EN/KO.
- [x] Nondeploy Debug/Release builds, focused/full native compatibility suites,
  review and documentation. Conventional commit/push follow verification.

Never deploy, launch the game, or check native/decompiled assets into the repo.
Use `-p:DeployMod=false` for every build/test. Preserve ordinary Deathmatch and
casual friendly-fire behavior; live guest validation remains a separate step.
