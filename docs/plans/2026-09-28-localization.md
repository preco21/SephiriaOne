# English/Korean JSON localization implementation plan

Goal: translate SephiriaOne's panel, help, command feedback, status and addon
notifications using editable English/Korean JSON catalogs and a local JSON language
selection. Language is not a multiplayer gameplay preset or a command alias.

Architecture: a Unity-independent `L` service owns bounded JSON parsing, cached
lookups, composite formatting, fallback and atomic reload. English source strings
serve as catalog keys. User-facing construction sites explicitly call `L.T` or
`L.F`; protocol keys, player names, identifiers, saved policy rows and native text
are never processed by broad substring/regex replacement. The game-provided
Newtonsoft.Json assembly avoids shipping another runtime dependency.

- [x] Add failing loader/format/config tests, implement the cached localization
  service with `en` default, `ko`, English fallback and validated placeholders.
  Seed missing config/catalog files from embedded defaults without overwriting edits.
- [x] Translate panel text and dynamic readouts, preserve command tokens and player
  data, refresh existing views on language changes, and reuse a Korean-capable game
  font without modifying native font assets.
- [x] Translate help/feedback/status and addon Rabbit descriptions/MP alerts.
  Keep internal serialization and gameplay checks independent of language.
- [x] Add `/one language en|ko|reload|status`, initialize from
  `Application.persistentDataPath/SephiriaOne/localization`, and document editing.
- [x] Verify catalogs, fallbacks, invalid JSON and formatting, language switching,
  English regressions and Korean command/state parity; review changes; build without
  deployment. Commit and push follow under the standing repository instructions.

All dotnet builds/runs use `-p:DeployMod=false`. No deployment or hotkey is added.
Guest-visible addon alerts use the host's selected language; guests need no addon
to receive their existing native text messages. Native game translations are outside
this change. Technical log identifiers and command/preset syntax remain stable.

## Verified outcome

349 keys in each bundled catalog; 47 loader cases, 976 catalog checks, 11 localized
feature checks and 757 runtime integration checks pass. Rabbit tooltip tests pass
35 existing + 6 language checks; potion tests pass 134 existing + 12 language checks.
Merchant (63 across 31 scenarios), room placement (34), starting-resource (71),
budget/inventory (66) and disconnect-boundary (14) suites pass. Pure policy tests
and installed-game native/UI/Harmony contracts pass. Debug and Release production
builds have zero warnings/errors with deployment disabled. The five-player idle
probe reports zero bytes/tick with no settings and with all settings active.

Independent review found a custom blank-translation edge case in existing
message-derived readiness/fallback checks. Reproduced it with failing tests,
rejected blank translations, guarded trailing-notice removal, and made native
fallback and control readiness explicit state. Reviewer rechecked the fix and
47/757 focused suites; no actionable findings remain. Native English/Korean
locales both use Galmuri; live rendering/playtesting remains unverified.
