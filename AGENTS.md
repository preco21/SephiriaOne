# Repository workflow

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
