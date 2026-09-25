# Starting-resource lifecycle fixtures

Run `dotnet run --project tests/SephiriaOne.ResourceHookTests -c Release -p:DeployMod=false` from the repository root.

This executable links the actual starting-resource hooks, planners, and policy
types against small data fixtures. It does not load Unity, execute the game, or
patch a live game process. Reflection invokes the actual grant adapters so that
checkpoints, saved zero balances, restart ownership, dice policy freezing, late
lobby leaf edits, explicit resets, per-player baselines, spending,
unload, and partial-write behavior remain covered.

The regular portable suite's `GameStartingResourceCompatibilityTests` separately
reads the installed game's IL and checks both transpilers and native SyncVars.
Neither fixture verifies multiplayer transport or guest rendering.
