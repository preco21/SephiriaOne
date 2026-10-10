# Compact settings panel

Scope: presentation only. Keep commands, validation, native input ownership,
draft isolation, gameplay state and synchronization unchanged.

- [x] Reduce the window from 800×480 to 760×420. Keep two rows of seven tabs,
  a readable two-column form, scrolling values and space for action feedback.
- [x] Use shared button captions for +, − and ×, with existing semantic action
  names and callbacks. Keep Save, Set, Reset, revive and match actions explicit.
- [x] Remove redundant instructions and repeated host/persistence explanations.
  Retain numeric limits, non-obvious timing, sample-potion exclusions, safety
  and compatibility messages. Shorten English and Korean together.
- [x] Build without deployment; run the existing panel, catalog and native UI
  contract checks. Review layout bounds and command bindings.

No game launch, deployment, new input hooks or per-frame work. Automated checks
do not establish actual Unity text rendering; manual EN/KO inspection remains
necessary after user deployment.

Verification: Debug and Release builds passed with zero warnings/errors. The
portable/native compatibility suite passed, including 3,066 bundled catalog
checks. Existing panel fixtures passed 10 geometry/intent and 12 lifetime checks.
Independent review identified two corrections, both applied: retain footer
autosizing without placeholder text, and keep Fountain's absolute Set-then-Add
semantics distinct from Stats in the compact help. The deathmatch contract test
now checks the retained start-control field and command rather than old wording.
