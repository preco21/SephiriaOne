# Grouped settings UI implementation

The user's requested layout is the approved design: seven sections in a vertical
left navigation rail; subtle rounded controls/panel surfaces and borders; square
filled checkboxes with softened corners; scrollbar arrows; no routine host/ready
counts. Preserve commands, drafts, safety warnings, input ownership and snapshots.

## Layout and architecture

- Window: 940 x 560 canvas units, clamped/scaled by existing window geometry.
  Sidebar at (12, 50), 210 x 494; content retains its 600-unit two-column forms
  at 1.15 scale, starting (232, 78), with 366 units of vertical room.
- Navigation registry keeps existing page IDs and snapshot requests. Groups:
  stats/resources [0,1,2,3,8], costumes [6,11], fun [7,10], PvP
  [9,13,14,15,16,18,19], growth [17], optimization [20], system [5,4,12].
- Use the user's exact Korean labels with separate descriptive English keys.
  Draw seven small section icons directly, avoiding unsupported font characters.
  System headers/buttons use muted colors; active tabs remain easy to find.
- Shared rounded Image mesh supplies border and fill without custom shaders,
  textures or per-frame polling. Geometry is cached or generated only on rebuild.
- Shared scrollbar construction supplies up/down buttons for all scroll regions,
  including dropdowns, readouts, participants and both balancing columns.
  Arrow movement uses pixel distance converted to clamped normalized position.
- Preserve aligned 25-unit input/action rows and existing form columns; give
  scrollable content more vertical space. Rabbit currently contains only its own
  healing and level-up potion settings; unrelated costume settings stay separate.

## Tasks

- [x] Shared rounded surface/icon geometry and geometry checks.
- [x] Shared control styling, scrolling factory, aligned label treatment.
- [x] Grouped navigation/layout and exact EN/KO labels; quiet status/update header.
- [x] Adapt all existing scroll consumers and remaining slider surfaces.
- [x] Verify registry reachability, scroll bounds, geometry, localization,
  installed UI contracts, build, and visual/layout inspection.
- [x] Update docs/version, independent review, Conventional Commit and push.

No deployment, gameplay change, new hotkey, or guest addon requirement.

Review caught and resolved short-dropdown resizing, offscreen keyboard navigation,
and global installation/restart warnings. Release build, portable window/shape/
localization checks and full installed contracts pass. Live Unity appearance and
interaction remain a manual follow-up; no deployment was performed.
