# Rabbit low-MP alert implementation plan

**Goal:** Tell the drinker when the Rabbit HP-potion MP fee prevents healing.

**Design:** Notify at the existing completed-drink rejection boundary. Show the
native timed system message locally; use the native floating-text RPC for an
unmodified guest, addressed exclusively to the drinker's current connection.
Text includes current and required MP. No polling, saved state, client addon,
assets, extra potion events, or changes to fee/death/Survival rules.

The installed game's system-message event is local-only. Its custom-message RPC
feeds shop events, not a generic message UI. The floating-text RPC already accepts
arbitrary text; Mirror sends both targeted and broadcast RPCs as the same native
RPC message. Validate its serialization contract before using a targeted send.
If optional feedback fails, the drink must still be rejected.

- [x] Extend potion fixtures with local/remote notices, rejection isolation,
  current fee/balance, repeated attempts, reconnect and feedback failure cases.
  Run them first to demonstrate the missing behavior.
- [x] Add a small native alert adapter and call it only for insufficient MP.
  Validate the native serializer/reader and targeted transport against installed
  assemblies; disable guest feedback on incompatibility without disabling the
  existing potion protections.
- [x] Run potion and installed-contract suites plus Debug/Release builds, always
  with `-p:DeployMod=false`. Review the final diff and update feature/history docs.

Completion workflow: commit and push the verified change using Conventional
Commits. Do not deploy. Visual/live guest confirmation still requires gameplay.

Results: three new notice cases failed before implementation; 134 potion cases
now pass. Both builds passed without warnings, as did 1,122 pure checks, installed
contracts, 682 runtime checks and 35 description checks. Review found no actionable
issues. No game binaries, decompiled sources or temporary probes are committed.
