# Repository workflow

## Research and implementation materials

- Agents and contributors should use `docs/*` as shared research and working
  material. Start with [the documentation index](docs/README.md), then read
  [general findings](docs/general-findings.md), [domain findings](docs/domain-findings.md)
  and the feature documents relevant to the task. Collected external sources are
  indexed in [references](docs/references.md).
- Prefer the current extension guide and the latest dated feature follow-ups
  over historical designs in `docs/plans/`, `docs/superpowers/plans/` or early
  investigation notes. Confirm behavior against current source and installed-game
  contracts; a recorded finding or passing fixture is not proof of live guest
  behavior or compatibility with a different game build.
- Record reusable findings, native symbols, evidence, limitations and useful
  references in the relevant `docs/` file when a task discovers or changes them.
  Link new material from the index. Keep game binaries, decompiled source and
  private logs outside the repository.
- Keep development builds non-deploying with `-p:DeployMod=false`. Do not run
  `scripts/Deploy-Mod.ps1` or copy output into the game unless the user explicitly
  requests deployment; the project otherwise deploys automatically after a build.

## Commit and push

- After completing requested repository changes and appropriate verification,
  automatically commit the relevant changes and push the current branch to its
  configured remote. The user has authorized this workflow; do not ask for
  confirmation again for routine commits and pushes.
- Follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):
  `type(optional-scope): description`. Use `feat` for new functionality, `fix` for
  bug fixes, and suitable types such as `docs`, `build`, or `chore` for other work.
  Mark breaking changes with `!` or a `BREAKING CHANGE:` footer when applicable.
- Keep commits focused. Stage only changes belonging to the requested work;
  preserve unrelated user changes. Do not commit game binaries, decompiled game
  source, build output, or credentials.
- Do not rewrite published history or force-push unless explicitly requested.
- If verification, committing, or pushing fails, report the concrete blocker and
  what remains incomplete. Do not claim a push succeeded without checking it.
