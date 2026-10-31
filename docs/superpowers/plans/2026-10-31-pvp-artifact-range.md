# PvP autonomous artifact range implementation plan

> Execute inline using the existing shared combat infrastructure; request an independent review before committing.

**Goal:** Limit automatic artifact attacks against hostile players to 5 m from
their current owner during balanced PvP, with a live configurable radius.

**Architecture:** Reuse the seven audited autonomous artifact identities and
Dark Cloud's separate strike boundary. A shared squared-distance predicate
filters only those target selectors, their homing, and their admitted impacts.
Native PvE, direct attacks, books, retaliation, planets, companions and other
excluded proc sources retain their existing range. No global faction change,
new network message, roster cache or update loop.

**Constraints:** Host-only, all shared PvP modes and live faction/selection
rules; active only with PvP balancing; inclusive distance boundary; default
5 m, configurable 0.5–50 m with two decimals; preserve native shorter ranges.
Already applied DoT continues. Builds/tests use `-p:DeployMod=false`.

- [x] Reproduce out-of-range damage and targeting in the existing executable
  combat fixtures (`tests/SephiriaOne.FriendlyFireTests/ArtifactRangeTests.cs`).
  Test the exact 5 m boundary, owner movement, mixed PvE/PvP candidates,
  Dark Cloud callback/FX suppression, homing loss, exclusions and zero allocation.
- [x] Add `PvpArtifactRangeSettings` (default-aware scalar, validation and
  squared radius), integrate `FriendlyFireSettings` and `FriendlyFireCommand`.
  `/one friendlyfire artifact-range <meters>|reset` changes only this value.
  Add v58 sparse preset support and live host validation/status.
- [x] Route explicit autonomous selectors through `AutonomousArtifactTarget`;
  route Chakram's mask through the equivalent helper; check current owner range
  at Dark Cloud strike and central damage admission, and Fire Feather homing.
  Validate the installed native IL before installing changed adapters.
- [x] Add a separate range row in the existing Artifacts / Debuffs scroll
  section, with Apply/Reset, EN/KO labels/help, cached refresh and draft cleanup.
- [x] Extend existing settings/preset and installed-game contract tests;
  run combat, runtime, panel/localization and installed-game suites plus Release
  build. Document coverage/limits in `docs/pvp-autonomous-artifacts.md`, link from
  the index, bump version, review and commit/push without deployment.

Evidence: installed managed assembly SHA-256
`c57a0daeab8e8d0af7066a344133eec4c57d8f303fd9e25da9410fbfc4cf1510`,
REA `ev_aea169428da293348307f450974cc2ab8e7f1ef5e1cb1308299bbf37b345fac7`.
Matching cached ILSpy source: selectors for Guillotine, Earring, Red Snake Eyes,
Phoenix Wing, Echo and Ice Bat; Chakram mask; Dark Cloud iterator and
`FireLightning`; `Bullet.AttackOnServer` only invokes its attack callback after
successful native damage. Reuse exact identity contracts, not broad categories.

Review follow-up: reproduced and fixed secondary debuffs that ran after the
damage scope ended, plus cached Earring/Guillotine delayed emissions. Independent
re-review found no remaining important bugs. Release build: zero warnings/errors.
Combat: 15,825 executable checks; runtime: 17,829 integration checks plus 69 Bat
checks; settings/presets, localization, panel and installed-game contracts passed,
including 41 compiled Harmony API references against the co-installed library.
No live multiplayer test and no deployment.
