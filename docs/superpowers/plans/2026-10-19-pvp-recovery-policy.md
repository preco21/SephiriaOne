# PvP recovery policy and defaults

User-authorized implementation plan, 2026-10-19.

**Goal:** Burn defaults to 0.9 seconds; Zombies default to 30% incoming damage;
active PvP suppresses Rabbit shared potion healing and all HP Steal from player
damage. PvE behavior returns through live policy reads, without stat restoration.

**Architecture:** Keep the existing per-hit Harvest boundary and per-effect
interval policy. Distinguish HP suppression from MP Harvest's existing 20%
balanced-PvP amount. Use shared current participation for Rabbit source and
recipient checks, including Tournament's current pair and Zombie teams.

**Scope:** PvP recovery restrictions apply regardless of the optional balancing
toggle. Preserve self potion healing, MP cost, infinite potion behavior, sample
exclusion, Survival options, unrelated heals, MP Harvest policy and PvE hits.
Neither an active participant nor a spectator may share potion healing with an
active participant. Spectators sharing with spectators remain native. Read
current authority/selection/mode policy at each heal, including callback changes.
Keep explicit saved settings; default/reset paths adopt the new values. Do not
deploy, launch the game or write temporary stat overrides.

- [x] Add regression coverage for default/reset/preset migration and each
  effect's independent interval; update only Burn and Zombie incoming defaults.
- [x] Test HP recovery removal at native Harvest sites for direct, reflected,
  debuff and companion-owned player hits; retain MP and nested PvE behavior.
- [x] Test Rabbit PvP source/recipient suppression, actual mode/selection rules,
  callback changes and immediate restoration; reuse shared participation gates.
- [x] Keep EN/KO descriptions and costume summaries accurate; document scope,
  saved settings and native evidence in feature documents.
- [x] Build with `-p:DeployMod=false`; run affected executable and installed-game
  contract suites and independent review. No deployment.

Release verification passed: 4,944 friendly-fire checks, 2,784 runtime checks
plus 69 Bat checks, 266 Rabbit hooks plus 12 localized alerts, and the portable
and installed-game compatibility suites. The independent review found no
concrete issues. Live host/guest gameplay was not exercised. Commit and push
follow the repository's standing authorized workflow.
