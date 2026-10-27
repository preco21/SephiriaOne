using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class CombatModeHooks
    {
        private const string Id = "SephiriaOne.CombatMode";
        internal static bool ValidateBattle(IEnumerable<CodeInstruction> instructions, bool start)
        {
            var code = instructions.ToList();
            var counter = AccessTools.Field(typeof(UnitAvatar), "isInBattle");
            var setter = AccessTools.PropertySetter(typeof(UnitAvatar), "NetworkisInBattle");
            int at = code.FindIndex(i => i.Calls(setter));
            return counter?.FieldType == typeof(sbyte) && setter != null &&
                code.Count(i => i.Calls(setter)) == 1 && at >= 6 &&
                code[at - 6].opcode == OpCodes.Ldarg_0 && code[at - 5].opcode == OpCodes.Ldarg_0 &&
                code[at - 4].opcode == OpCodes.Ldfld && Equals(code[at - 4].operand, counter) &&
                code[at - 3].LoadsConstant(1) && code[at - 2].opcode == (start ? OpCodes.Add : OpCodes.Sub) &&
                code[at - 1].opcode == OpCodes.Conv_I1;
        }
        internal static bool ValidatePresentation()
        {
            var assembly = typeof(UnitAvatar).Assembly;
            var getter = AccessTools.PropertyGetter(typeof(UnitAvatar), "IsInBattle");
            bool CallsBattle(string type, string method, bool closes = false)
            {
                var owner = assembly.GetType(type);
                var target = owner == null ? null : AccessTools.DeclaredMethod(owner, method);
                if (getter == null || target == null) return false;
                var code = PatchProcessor.GetOriginalInstructions(target).ToList();
                return code.Any(i => i.Calls(getter)) && (!closes || code.Any(i => i.operand is MethodInfo m && m.Name == "Close"));
            }
            var field = AccessTools.Field(typeof(UnitAvatar), "isInBattle");
            var setter = AccessTools.PropertySetter(typeof(UnitAvatar), "NetworkisInBattle");
            if (field?.FieldType != typeof(sbyte) || setter == null) return false;
            var sync = PatchProcessor.GetOriginalInstructions(setter).ToList();
            if (!sync.Any(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, field)) ||
                !sync.Any(i => i.LoadsConstant(67108864L)) ||
                !sync.Any(i => i.operand is MethodInfo m && m.Name == "GeneratedSyncVarSetter")) return false;
            foreach (string handler in new[] { "HandleOnOpenCharacterStatusPanel", "HandleOpenStatsPanel", "HandleOpenPassivePanel", "HandleOpenLevelUpPanel", "HandleOnOpenMapPanel" })
                if (!CallsBattle("PlayerInputController", handler)) return false;
            return CallsBattle("UI_MapPanel", "Update", true) && CallsBattle("UI_NewWorldMapPanel", "Update", true) &&
                CallsBattle("UI_Map_Room", "TeleportToRoom");
        }

        internal static void Install()
        {
            var start = AccessTools.DeclaredMethod(typeof(UnitAvatar), "LocalStartBattle", Type.EmptyTypes);
            var stop = AccessTools.DeclaredMethod(typeof(UnitAvatar), "LocalStopBattle", Type.EmptyTypes);
            var travel = AccessTools.DeclaredMethod(typeof(PlayerAvatar), "MoveFloorViaWorldmap",
                new[] { typeof(string), typeof(bool), typeof(string) });
            if (start == null || stop == null || travel == null || start.IsStatic || stop.IsStatic ||
                !ValidateBattle(PatchProcessor.GetOriginalInstructions(start), true) ||
                !ValidateBattle(PatchProcessor.GetOriginalInstructions(stop), false))
                throw new InvalidOperationException("Native combat counter/travel contract changed.");
            var harmony = new Harmony(Id);
            try
            {
                foreach (var method in new[] { start, stop }) harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(CombatModeHooks), nameof(BeforeBattle)),
                    finalizer: new HarmonyMethod(typeof(CombatModeHooks), nameof(AfterBattle)));
                harmony.Patch(travel, prefix: new HarmonyMethod(typeof(CombatModeHooks), nameof(BeforeTravel)));
            }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() => new Harmony(Id).UnpatchAll(Id);
        private static void BeforeBattle(UnitAvatar __instance) => CombatModeRuntime.BeforeNativeBattle(__instance);
        private static Exception AfterBattle(UnitAvatar __instance, Exception __exception)
        { CombatModeRuntime.AfterNativeBattle(__instance); return __exception; }
        private static bool BeforeTravel(PlayerAvatar __instance) => !CombatModeRuntime.Restricted(__instance);
    }
}
