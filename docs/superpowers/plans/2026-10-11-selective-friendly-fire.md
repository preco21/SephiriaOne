# Selective friendly fire implementation plan

**Goal:** Temporary connection-bound player selections shared by combat and deathmatch.

**Architecture:** A single host-side participation registry issues exact-lifetime
receipts. Combat reads it at targeting and damage boundaries; deathmatch captures
receipts for enrollment and recovery. Commands and the panel share one service.

**Constraints:** Empty selection includes everyone. Nonempty selection requires
both controlling players selected. The master switch still gates damage. Reset
selection restores everyone mode. Selection edits stop deathmatch and recover
pending participants before mutation. Never persist selections or deploy builds.

- [x] Registry and lifecycle hooks: invalidate before disconnect/destruction,
  connection reuse, initialization, server shutdown, and run/controller changes.
  Test actual receipts with exact native connection-map and identity contracts.
- [x] Combat: gate direct hits, reflection, AI, item searches, status ticks and
  callback re-entry. Preserve an admitted lethal hit's game-over protection.
- [x] Commands and snapshots: `/one friendlyfire players`, `player <id> on|off`,
  `players all|reset`; UI passes captured receipts to reject stale clicks. Test guest
  rejection, reset, presets, and unavailable unrelated feature state.
- [x] Deathmatch: include only eligible players; capture lifetime for pending
  deaths and recovery. Test excluded deaths, reconnects, reset during death, and
  immediate revival ordering when selection changes.
- [x] Panel: scrolling player checkboxes, Select All and Reset selection; English/Korean
  labels. Reuse rows between refreshes; no repeated UI tree creation.
- [x] Verify RuntimeTests, FriendlyFireTests, PanelTests, installed-game contracts,
  Debug/Release builds and performance probe with `-p:DeployMod=false`. Review,
  document evidence/limits. Finish with conventional commit and normal push.

The first command regression was run before implementation and failed at
`Selection command lists current players`, as expected.
