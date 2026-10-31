# Play Tag role and detonation visuals

**Goal:** A persistent stock green X on the current carrier, one subtle red
impact flash per role assignment, and one double-scale Leaf Explosion only on
bomb detonation. Host and unmodified guests use existing native replication.

**Design:** Generalize the audited Zombie marker lifecycle into a shared combat
role presentation adapter. Match token/connection receipts and current role
predicate determine ownership. Role changes remove the old marker and create
one on the new carrier, including a dead carrier awaiting respawn. Readiness
loss removes the marker; recovery may restore it without replaying assignment.
Use a dedicated assignment generation and record it before emitting the pulse
to prevent duplicate or reentrant transitions. Keep ordinary kill FX unchanged.

Use the audited small red impact transport at 0.75 scale for assignment. For
detonation, scale the one isolated native MoneyWhirlwind instance to 2 before
network spawn. Validate Mirror's initial scale transport and native rendering;
no second object, extra damage, leaves spent or doubled sound is introduced.

- [x] Add runtime regression for marker assignment/transfer, corpse/respawn,
  readiness, reconnect, interruption, round changes and exactly-once pulse.
- [x] Share existing Zombie marker and impact adapters; integrate Play Tag
  role predicate, transitions and cleanup without a new polling controller.
- [x] Double explosion visual scale before native spawn; keep existing consumed
  bomb-event gate and owned lifetime. Add installed-game transport contracts.
- [x] Run runtime, role-adapter, friendly-fire, Play Tag and native compatibility
  checks plus non-deploying Release build. Update docs/version, independent
  review, commit and push. No deployment or claimed live guest test.

Existing installed assembly identity and prefab evidence are recorded in
`docs/zombie-role-visual.md`, `docs/play-tag.md` and the native contract tests.
Mirror initial spawn scale, not host-only renderer changes, must carry the
larger explosion to stock clients. Leaf Explosion is never a role-change cue.

Completed verification and independent read-only review found no actionable
regressions. Runtime integration: 17,859 checks plus 69 Bat checks; shared role
adapter: 36; friendly-fire hooks: 15,825; audio: 44; bomb policy: 20,680.
Installed native transport/scale and Harmony contracts passed. Release 0.105.0
build: zero warnings/errors. No deployment; live host/guest appearance pending.
