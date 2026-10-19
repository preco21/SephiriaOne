# Large Potion supplies

Use native Large Potion ID 1 and native inventory replication. Manual command
`/one friendlyfire potion` and a Supplies button grant one to every ready player
who lacks it. Check potion belt/grid, sub-bag and temporary inventory; use native
overflow only after a definite full-inventory result, never after exceptions.

Automatic supplies default off (`auto-potion on|off|reset`). Reuse the shared
reconciliation coordinator and connection receipts, isolated from critical stat
reconciliation. Observe entry/eligibility and the shared match token, not item
counts, so consumption does not trigger refills. Match membership includes
countdowns and Tournament's waiting roster. Rejoins get a fresh ownership check.

1. Add parser/preset and inventory/lifecycle regression tests; confirm failure.
2. Implement guarded native grants and shared automatic entry reconciliation.
3. Add compact Supplies controls, English/Korean strings and saved settings.
4. Build without deployment, run focused and shared regression suites, review,
   record native findings, commit and push.

Live stock-guest UI/replication still requires a multiplayer smoke test. Never
deploy or start the game as part of this work.
