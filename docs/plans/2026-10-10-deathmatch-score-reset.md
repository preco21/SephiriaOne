# Deathmatch score reset

**Goal:** Add a host command and Deathmatch-tab button that reset all match K/D/A
without stopping the match, changing friendly fire, or restarting either timer.

**Design:** `/one deathmatch resetkda` and **Reset K/D/A** call the same scoped
runtime action. Accept it during a current opening countdown or active match;
reject guests, idle/expired matches, teardown and stale run/session identities.
Zero scores in place so rankings retain participant identity/order, including
departed players. Clear pre-reset assist history and advance the KDA epoch so
in-flight damage/death callbacks cannot restore earlier points. Keep ordinary
toggle-reset behavior unchanged: final standings still need their captured scores.
Broadcast one reset notice through the existing native chat path for stock guests.

**Alternatives:** Restarting the match changes timers and revives players;
replacing score objects breaks retained participant/reconnect identity. An in-place
score reset plus attribution invalidation avoids both problems.

- [x] Add command-driven regressions that fail before implementation: all scores,
  post-reset kills/assists, in-flight callbacks, disconnected/rejoining players,
  timers, warmup, authority/scope/expiry and pending joins/faults.
- [x] Add the shared KDA primitive and scoped runtime action; wire command and UI.
- [x] Add EN/KO labels/help/notice and update deathmatch documentation.
- [x] Run runtime/combat, allocation, panel and installed-game compatibility tests;
  build Debug/Release, always with `-p:DeployMod=false`.
- [x] Independent read-only review. Fixed its NPC/companion stale-notice finding:
  new regressions fail before the shared notice-epoch fix and pass afterward.
  Follow-up review found no remaining issue. Finish with a Conventional Commit
  and push `main` after verification.

No deployment, game launch, new hooks, per-frame scans, settings persistence or
network schema. Live guest rendering remains a manual verification limit.
