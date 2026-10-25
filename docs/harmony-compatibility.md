# Co-installed Harmony compatibility

Updated 2026-10-25, addon `0.95.2`.

## PvP initialization and Dark Cloud observer FX (`0.95.2`)

The 2026-10-25 `Player.log` from addon assembly `0.95.0` reports:

```text
Friendly fire unavailable: System.MissingMethodException:
HarmonyLib.AccessTools.MethodDelegate<T>(MethodInfo, object, bool, Type[])
```

This came from the observer-FX adapter introduced in `0.94.5`, not the settings
UI. The co-installed RaidRaid library identifies as `0Harmony 2.9.0.0` and lacks
that helper signature. The compiled-addon API audit against that exact DLL
reproduced the failure; it was the only missing compiled Harmony member.

`FriendlyFireDarkCloudVisualHooks` now binds the native sender with framework
`Delegate.CreateDelegate`, matching our other native FX adapters. The binding is
an open instance delegate: each call supplies the cloud, recipient, position and
variant flags. It is cached once at installation and retains the existing sender
hook, observer guards and single native owner delivery.

The previous `try/catch` was in the same method as the unresolved member call.
Mono could throw while compiling that method, before entering its catch, so the
exception reached the outer combat installer and rolled back friendly fire.
The optional installation body now sits in a separate non-inlined method called
inside `TryInstall`'s catch boundary. Optional initialization failures remove
its own patches/cache and warn, while leaving combat initialization available.
Native contract checks are retained; no compatibility check is bypassed.

Regression coverage now executes the production FX installer (only native IL
validation is substituted in the fixture), checks dependency failure and failure
after patch installation, owned cleanup, recovery, and observer routing. The
installed-game test also creates the framework delegate against the real native
RPC signature without executing it. The complete API audit passes all 41
compiled Harmony references against the installed alternate DLL. Existing warm
forwarding still allocates zero managed bytes across 3,000 nine-observer strikes
in the .NET fixture; the change adds no per-frame or per-hit reflection.

These checks do not execute HarmonyX's detours in the game's Mono runtime or
confirm live guest rendering. After manual installation, fully restart the game
and verify PvP controls and Dark Cloud guest effects. No addon was deployed and
no co-installed library was replaced. A DLL's metadata version label can differ
from its assembly version; this log showed metadata `0.94.0`, but that label was
not the cause of the missing-method exception.

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
