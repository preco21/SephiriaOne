using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    internal static class ItemRestrictionHooks
    {
        private const string Owner = "preco21.SephiriaOne.ItemRestrictions";
        private static Harmony harmony;
        internal static bool Available { get; private set; }
        internal static bool ValidateContracts()
        {
            var bound = AccessTools.DeclaredMethod(typeof(DungeonManager), nameof(DungeonManager.BoundOnServer));
            var own = AccessTools.DeclaredMethod(typeof(DungeonManager), nameof(DungeonManager.OwnRestrictionOnServer));
            var save = AccessTools.DeclaredMethod(typeof(DungeonManager), "SaveCurrentSessionData", new[] { typeof(string) });
            bool Key(MethodInfo method, string key) => method != null && method.ReturnType == typeof(void) &&
                PatchProcessor.GetOriginalInstructions(method).Count(i => Equals(i.operand, key)) == 1 &&
                PatchProcessor.GetOriginalInstructions(method).Any(i => i.operand is MethodInfo m && m.Name == "set_Item");
            return Key(bound, "Bound") && Key(own, "OwnRestriction") && (int)ERestrictedOwnType.StartingItem == 1 &&
                AccessTools.Field(typeof(DungeonManager), "globalItemStatTable")?.FieldType == typeof(SyncDictionary<string, string>) &&
                save != null && save.ReturnType == typeof(void) &&
                PatchProcessor.GetOriginalInstructions(save).Count(i => Equals(i.operand, "GlobalItemStatCount")) == 1 &&
                PatchProcessor.GetOriginalInstructions(save).Count(i => i.operand is FieldInfo f && f.Name == "globalItemStatTable") == 2 &&
                AccessTools.PropertySetter(typeof(Item), "NetworkisBound") != null;
        }

        internal static void Install()
        {
            if (Available) return;
            try
            {
                if (!ValidateContracts()) throw new InvalidOperationException("Native item restriction/save contracts changed.");
                harmony = new Harmony(Owner);
                foreach (string name in new[] { nameof(DungeonManager.BoundOnServer), nameof(DungeonManager.OwnRestrictionOnServer) })
                    harmony.Patch(AccessTools.DeclaredMethod(typeof(DungeonManager), name),
                        prefix: new HarmonyMethod(typeof(ItemRestrictionHooks), nameof(PrepareScope)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(DungeonManager), "SaveCurrentSessionData", new[] { typeof(string) }),
                    postfix: new HarmonyMethod(typeof(ItemRestrictionRuntime), nameof(ItemRestrictionRuntime.SaveNativeRestrictions)));
                Available = true;
            }
            catch (Exception error)
            {
                Uninstall();
                Debug.LogWarning("[SephiriaOne] Item restriction compatibility checks failed: " + error);
            }
        }

        private static void PrepareScope()
        {
            if (!NetworkServer.active) return;
            try { SessionSettings.EnsureResourceScope(); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Item restriction session setup failed: " + error); }
        }

        internal static void Uninstall() { harmony?.UnpatchAll(Owner); harmony = null; Available = false; }
    }
}
