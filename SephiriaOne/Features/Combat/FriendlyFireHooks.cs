using System;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireHooks
    {
        private const string Id = "SephiriaOne.FriendlyFire";
        internal static void Install()
        {
            var apply = AccessTools.DeclaredMethod(typeof(UnitAvatar), "ApplyDamage", new[] { typeof(DamageInstance) });
            var die = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Die", new[] { typeof(int), typeof(DamageInstance) });
            if (apply == null || apply.ReturnType != typeof(EApplyDamageResult) || die == null ||
                !FriendlyFireTranspiler.Validate(PatchProcessor.GetOriginalInstructions(apply)))
                throw new InvalidOperationException("Native allied damage/defense contract changed.");
            var harmony = new Harmony(Id);
            try
            {
                harmony.Patch(apply, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeHit)),
                    transpiler: new HarmonyMethod(typeof(FriendlyFireTranspiler), nameof(FriendlyFireTranspiler.Rewrite)),
                    finalizer: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterHit)));
                harmony.Patch(die, prefix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.BeforeDeath)),
                    postfix: new HarmonyMethod(typeof(FriendlyFireRuntime), nameof(FriendlyFireRuntime.AfterDeath)));
            }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() { new Harmony(Id).UnpatchAll(Id); FriendlyFireRuntime.Clear(); }
    }
}
