# Tournament announcement context

Goal: add progression round and draw-rematch context to the pre-fight bubble
without changing scheduling, timers, scoring, damage or the start announcement.

The existing scheduler owns both wins and unordered-pair attempts. Derive
`PendingRound = 1 + min(wins[first], wins[second])` for a reserved pair, otherwise
zero. Derive `PendingIsRematch` from a previous completed attempt for that pair:
both entrants remaining eligible proves that attempt was unresolved. No extra
mutable history or KDA dependency is needed. Counting participation would
incorrectly promote draws; global match numbering would mislabel fresh entrants.

Format complete EN/KO templates in `CombatMatchTournament.PairMessage`, with
round and rematch context captured before the first send for both recipients.
Keep player names as formatting arguments. Korean uses `재대결`. Retry-countdown
interruptions do not create completed attempts; fresh tournaments own fresh state.

- [x] Update runtime countdown expectations and observe the missing-context
  failure with `dotnet run --project tests/SephiriaOne.RuntimeTests -c Release -p:DeployMod=false`.
- [x] Add scheduler properties and tests in `TournamentSchedule.cs` and
  `tests/SephiriaOne.TournamentTests/Program.cs`: first match, multiple draws,
  uneven wins, rounds 2/3/4, withdrawal, pending reads and fresh brackets.
- [x] Update `CombatMatchTournament.cs` and both localization catalogs; cover
  EN/KO first/rematch bubbles, later rounds, KDA reset and countdown interruption.
- [x] Run scheduler/runtime, catalog/native contract suites and Debug/Release
  builds with deployment disabled. Review the diff and update `docs/tournament.md`,
  localization notes, version and index.

Delivery: conventional commit and push per AGENTS after final review.

No new native hooks, guest addon, per-frame scan or persistent setting. Existing
stock-client bubble transport and host-selected language remain in use. Live
multiplayer rendering is outside automated verification; do not deploy or launch.
