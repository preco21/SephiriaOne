# Dedicated PvP Balance panel

Approved: dedicated tab within the existing draggable window. General controls
on the left; taller debuff/Dark Cloud editor on the right. Combat retains player
selection, friendly-fire controls, recovery actions and supplies.

## Design

- Add page 18, displayed directly after Combat without renumbering existing
  routes. Nineteen tabs use three rows of seven wider buttons.
- Window 760x560; page 600x250 at existing 1.25 scale. Preserve the existing
  canvas fitting, drag, hotkey and native input-stack behavior.
- Remove nested Combat/Balancing/DoT/Supplies buttons. General balancing has no
  inner scroll; each debuff row has a full-width label, input, Apply, and its
  existing Keep schedule checkbox. Dark Cloud retains only its interval.
- Reuse commands, per-field drafts, compatibility/authority gates, localization
  and page/session/language teardown. Page 18 requests SnapshotContent.None.
  No gameplay changes, preset migration, additional polling or new UI controller.

## Work

- [x] Update existing panel routing/compatibility checks for page 18 and moved
  build/refresh paths; retain command, draft and recovery contracts.
- [x] Move fields/refresh ownership into SettingsPanelPvpBalance; update Combat,
  DoT and participant layouts; update EN/KO labels and documentation.
- [x] Run non-deploying Release build, panel/localization/runtime and installed
  native compatibility checks; independent review.

Verification: Release build 0 warnings/errors; 12 launcher/lifetime and 16
geometry/checkbox checks; 50 localization checks; 13,796 runtime checks plus
69 Bat checks; portable/installed-game contracts including 4,344 catalog checks.
Review found no actionable issues. Actual Unity rendering/input remains a live
verification task. No deployment. Conventional commit/push follows verification.

Alternatives: a separate draggable window would duplicate input/lifecycle
ownership; another compact Combat subsection would preserve the current space
problem. Neither is needed for the requested dedicated tab.
