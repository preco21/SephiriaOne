# Co-installed Harmony compatibility

Updated 2026-10-13, addon `0.51.3`.

## Death, revive and private kill notices

The reported missing guest death/revive effects were traced to the host's
`Player.log` while running addon `0.51.0`. Each optional adapter logged the same
`MissingMethodException` before it could send its native message:

```text
HarmonyLib.CodeInstructionExtensions.LoadsConstant(HarmonyLib.CodeInstruction,string)
```

Affected validators: `FriendlyFireDeathVisuals.ValidateCode`,
`NativeReviveVisuals.ValidateCode`, and `NativePlayerChat.ValidateCode`. This also
prevented private killed-by/matchup messages. Their caught exceptions deliberately
left combat, KDA, respawn countdowns and actual revival working. Validation is
cached, so the affected presentation paths stayed unavailable for that addon
lifetime. A visible effect on one's own death can still be the game's native
local-only crash, which does not demonstrate that the addon broadcast succeeded.

The project builds with and embeds Harmony `2.4.2`. A co-installed addon supplies
HarmonyX (`0Harmony` assembly version `2.9.0.0`), whose public API lacks the string
overload. Loading our embedded assembly does not guarantee all addon references
bind to it in the game's shared Mono process. Do not replace another addon's
library or infer helper availability from the version number.

The fix compares `OpCodes.Ldstr` and the exact operand at the four string-check
sites. Integer checks retain their compatible existing helper. Native contract
validation, failure handling, payloads, observer/owner delivery, success guards
and reset semantics remain unchanged. No additional packets, per-frame work or
guest installation requirement are introduced.

## Regression procedure

The main compatibility runner accepts an optional third argument pointing to an
alternate `0Harmony.dll` (and its adjacent dependencies):

```powershell
dotnet run --project tests/SephiriaOne.Tests -c Release -p:DeployMod=false -- $gameManaged $addonDll $alternateHarmonyDll
```

Use the normal managed/addon paths from [general findings](general-findings.md).
The supplied DLL is only read, never copied into or changed in the game.
`GameHarmonyRuntimeTests` scans compiled addon methods, including generated
lambdas, for Harmony member references. An isolated assembly context compares
their declaring types, signatures, static/instance shape and return types to the
alternate library. This catches newer helper calls hidden inside lazy validators,
which ordinary tests using the build package do not expose.

The audit failed on the missing string overload before the production fix and
passed afterward for all 28 distinct compiled Harmony API references. The normal
portable/installed-game tests still execute the native validators with Harmony
2.4.2 and test rejected payload/receiver mutations. These two checks establish
API availability and retained contract semantics, not live Mono execution:
HarmonyX 2.9's detour initialization cannot run on the .NET 10 test host. It should
not be swapped into the normal test process as if it were the same runtime.

Debug/Release builds passed with zero warnings/errors and deployment disabled.
The full portable/native contract suite, 1,002 executable combat checks, 1,905
runtime integration checks and 69 Bat lifecycle checks passed. Independent
read-only review found no actionable issues and repeated the alternate API audit.

Live guest rendering still needs verification after installing `0.51.3` and
fully restarting the game. Check a guest's death, another observer's view, the
three-second successful respawn pillar in both match modes, and private killed-by
chat. No addon was deployed or game launched during this fix.

Related: [death visuals](friendly-fire-death-visuals.md),
[automatic respawn visuals](respawn-visuals.md),
[private kill notices](friendly-fire-killed-by.md).
