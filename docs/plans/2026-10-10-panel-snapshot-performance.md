# Panel snapshot performance plan

**Goal:** Reduce recurring work while the settings window is open without changing
gameplay, synchronization timing, permissions, current values or diagnostics.

**Design:** Request only the snapshot sections displayed by the selected tab.
Keep one capture path and the existing complete-snapshot API. Every capture still
reads player readiness, identity, feature availability and current policy. Omitted
dictionaries are immutable empty views. Diagnostics imply all sections. No native
values or formatted messages are cached across captures.

**Alternatives:** A cross-frame cache needs invalidation for native edits, language,
re-entry and faults; slowing polling affects freshness. Section selection avoids
both risks and preserves the existing module boundaries.

**Baseline:** `c186f34`, five-player Release fixture: unchanged synchronization
0 B/tick; full report 116,558 B/refresh; non-Status 25,432 B/refresh. At four
refreshes per second, even tabs displaying only toggles build 27 stats and five
resource descriptions per player, including inventory/budget scans.

## Implementation and evidence

- [x] Inspect periodic controllers, combat/companion guards and snapshot consumers;
  run the existing performance fixture before changes.
- [x] Add explicit section selection to `Controls/SettingsSnapshot` and
  `Session/SessionSettingsSnapshot`; retain the old full/compact API defaults.
- [x] Map tabs to sections in `UI/SettingsPanel`; unknown tabs default to a full
  capture so future pages cannot silently lose required values.
- [x] Extend `SnapshotDetailTests` across existing readiness, fault, native edit,
  preset, authority and localization scenarios; verify detached dictionaries,
  no writes/revision changes, page selection and unchanged full reports.
- [x] Add allocation budgets for section-specific captures and demonstrate their
  failure when section requests are ignored, then their success after the change.
- [x] Run runtime fixtures/perf checks, panel tests, Debug/Release builds and the
  installed-game compatibility runner, all with `-p:DeployMod=false`.
- [x] Review the diff and record measured results/limits in `performance-review.md`.
  The independent read-only review reported no findings and reran the runtime and
  allocation suites successfully. Finish with a Conventional Commit and push `main`.

No deployment, game launch, changes to refresh cadence, game hooks, network writes,
merchant schedule/random ordering, command arithmetic or saved preset format.
Measurements are .NET fixtures, not live Unity frame-rate or guest-delivery proof.

Evidence: the base-record budget failed at 25,432 B/refresh when the new selection
parameter forwarded to the old complete-value capture. The implemented selector
passes at 1,760 B; Stats/Choices/Resources/Presets are 8,000/3,040/11,200/8,232 B.
Both builds and all listed suites pass; detailed results are recorded in
[the performance review](../performance-review.md).
