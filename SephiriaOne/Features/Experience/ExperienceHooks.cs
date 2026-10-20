using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // One authority boundary for native XP pickups, fountain/dev rewards,
    // recovered XP on rejoin, and stock clients' CmdAddExp requests.
    internal static class ExperienceHooks
    {
        private const string Owner = "preco21.SephiriaOne.Experience";
        private static Harmony harmony;
        public static bool Available { get; private set; }

        public static void Install()
        {
            Uninstall();
            try
            {
                if (!ValidateNative()) throw new InvalidOperationException("Native XP grant boundary changed.");
                harmony = new Harmony(Owner);
                harmony.Patch(GrantMethod(), prefix: new HarmonyMethod(typeof(ExperienceHooks), nameof(BeforeGrant)));
                Available = true;
            }
            catch (Exception error)
            {
                Uninstall();
                Debug.LogWarning("[SephiriaOne] XP restriction unavailable; native XP continues: " + error);
            }
        }
        public static void Uninstall()
        {
            Available = false;
            try { harmony?.UnpatchAll(Owner); harmony = null; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] XP hook removal failed: " + error); }
        }
        private static MethodInfo GrantMethod() =>
            AccessTools.DeclaredMethod(typeof(LevelController), "LocalAddExp", new[] { typeof(int) });
        private static bool Calls(MethodBase caller, MethodInfo target) => caller != null && target != null &&
            PatchProcessor.GetOriginalInstructions(caller).Any(i => i.Calls(target));

        private static bool ValidateNative()
        {
            var grant = GrantMethod();
            if (grant == null || grant.IsStatic || grant.ReturnType != typeof(void)) return false;
            var type = typeof(LevelController);
            var initialize = AccessTools.DeclaredMethod(type, "LocalInitialize", new[] { typeof(int), typeof(int) });
            return Calls(AccessTools.DeclaredMethod(type, "AddExp", new[] { typeof(int) }), grant) &&
                Calls(AccessTools.DeclaredMethod(type, "UserCode_CmdAddExp__Int32", new[] { typeof(int) }), grant) &&
                Calls(grant, AccessTools.PropertySetter(type, "NetworkcurrentExp")) &&
                Calls(grant, AccessTools.DeclaredMethod(type, "LevelUpOnServer", Type.EmptyTypes)) &&
                Calls(initialize, AccessTools.PropertySetter(type, "NetworkcurrentExp")) && !Calls(initialize, grant);
        }

        private static bool BeforeGrant(LevelController __instance)
        {
            if (!Available || !NetworkServer.active || !__instance) return true;
            try
            {
                var settings = SessionSettings.ExperienceForUse;
                if (!settings.Block) return true;
                var player = __instance.Avatar;
                // Do not wait for the roster/inventory to finish enrolling: native
                // preserved-XP grants can run inside player initialization itself.
                return !player || !settings.Blocks(player.currentCostume);
            }
            catch (Exception error)
            {
                Available = false;
                Debug.LogWarning("[SephiriaOne] XP restriction disabled after an error; native XP continues: " + error);
                return true;
            }
        }
    }
}
