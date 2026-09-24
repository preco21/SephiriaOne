using System;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    internal static class SessionBoundaryHooks
    {
        private const string Owner = "preco21.SephiriaOne.SessionBoundary";
        private static Harmony harmony;

        public static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(PlayerSpawner), nameof(PlayerSpawner.AddDimensionPocketItemsOnServer), new[] { typeof(int[]) });
                if (target == null || target.ReturnType != typeof(void))
                    throw new MissingMethodException("PlayerSpawner.AddDimensionPocketItemsOnServer(int[]) changed.");
                instance.Patch(target, prefix: new HarmonyMethod(typeof(SessionBoundaryHooks), nameof(BeforeFountainGrant)));
                harmony = instance;
            }
            catch
            {
                instance.UnpatchAll(Owner);
                throw;
            }
        }

        public static void Uninstall()
        {
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        private static void BeforeFountainGrant()
        {
            // Native loadout edits and starting a run can arrive in the same
            // frame. Reconcile before the game clamps/grants the saved items.
            SessionSettings.BeforeNativeRead("Fountain grant");
        }
    }
}
