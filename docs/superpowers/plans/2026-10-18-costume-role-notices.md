# Costume role notices implementation plan

**Goal:** Concise, localized, current-state costume summaries for Wing-Eared
Rabbit and the supported companion costumes (Mole, Farmer Squirrel, Turtle).

**Design:** Private native chat is the default presentation, as stated while the
optional display preference remained unanswered. It supports stock guests without replacing combat bubbles.
Tooltip-only presentation cannot reach stock guests; overhead bubbles compete
with respawn/bomb countdowns. Use the existing session reconciliation loop after
stat maintenance, with a read-only per-connection presentation cache. No new
gameplay hooks, RPC definitions, persisted settings or stat writes.

Rabbit summaries include only compatible/enabled infinite HP potion, MP fee,
shared healing and level-up potion features. Share radius currently has no
configurable setting: extract the actual 10-world-unit/5-tile runtime constant
into shared settings so descriptions and healing cannot drift. Preserve Sample
exclusion. Companion summaries use enabled, actually applied owner modifiers;
show configured multipliers, native units for add/set settings, and only a
registered enabled Collin starting gift. Do not advertise suspended/no-op stats.

Messages update after costume, applied-stat, policy, compatibility or language
changes. Compare rendered content to avoid unrelated-command spam. Clear receipt
state on departure/scope loss; a new connection always receives a fresh summary.
Wait for readiness and successful native dispatch. Keep each line within the
native 120-character limit. Steady-state inspection must allocate no strings.

- [x] Add runtime regressions for recipient targeting, live settings, resets,
      costume switches, rejoin, loading, unavailable features and applied stats.
- [x] Implement grouped role formatter/cache plus shared session integration.
- [x] Extract the healing radius constant; share it with the existing tooltip.
- [x] Add EN/KO catalogs and contract checks for native message preservation.
- [x] Run non-deploying build, runtime, Rabbit, Collin and native contract suites;
      review changes, document findings and increment version.

Verification: Release build has zero warnings/errors; 2,465 runtime integration
checks, 257 Rabbit hook checks, 58 Rabbit description checks, 34 Collin lifecycle
checks, and portable tests including 3,678 catalog checks pass. Installed-game
contracts and all 33 alternate Harmony API references pass. The stable-role
fixture allocates zero bytes over 1,000 observations. Independent review found
no actionable issues. Live host/guest display remains unverified. Commit/push
follow the authorized repository workflow.

All dotnet commands use `-p:DeployMod=false`. Do not deploy or launch the game.
