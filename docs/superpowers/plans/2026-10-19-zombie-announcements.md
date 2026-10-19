# Zombie faction and conversion announcements

Implement the user's requested presentation changes using existing host/stock
guest chat transport. `[Z]` and `[H]` use basic ASCII characters already present
in native English/chat text, avoiding an unverified biohazard glyph. Translate
the surrounding role names in EN/KO.

- [x] Add failing runtime tests for named single/multiple starting announcements,
  long-name packing (120-character native limit), Zombie-only opening bubbles,
  conversion infector/timer/mission, and no generic respawn overlap.
- [x] Add focused Zombie text formatting. Reuse captured death attribution for
  infector names and the shared respawn deadline (currently one second).
  Defer Zombie death countdown presentation until death finalization decides
  infection; use one combined bubble, with no separate floating respawn text.
  Keep ordinary deaths and other modes unchanged; preserve existing K/D/A logs.
- [x] Mark faction names in status, results, announcements and relevant UI.
  Update EN/KO catalogs and documentation; bump version to 0.66.1.
- [x] Verify Release build, runtime/friendly-fire and installed-game contracts
  with `-p:DeployMod=false` and independent review. Commit/push follows the
  repository workflow. Do not deploy or launch game.

Lifecycle checks include opt-in environmental conversion without inventing a
killer, pending revival/next death, readiness delays, callback interruption,
death exceptions, and cleanup. Match timing, teams and gameplay do not change.

Verification: initial announcement test failed before implementation. Independent
review found that an opening callback could infect a Human before the role-message
loop, overwriting their conversion notice; a new regression failed and passed
after freezing the initial recipients and validating their current live role.
Final Release build has zero warnings/errors, 3,163 runtime checks plus 69 Bat
checks pass, 4,944 friendly-fire checks pass, and portable/localization plus
installed native/UI/HarmonyX contracts pass. Live host/guest UI remains untested.
