using System;
using System.Reflection;
using HarmonyLib;
using TMPro;

namespace SephiriaOne
{
    // Loaded only after RabbitDescriptionFeature has bootstrapped embedded Harmony.
    internal static class RabbitDescriptionHooks
    {
        private const string Owner = "preco21.SephiriaOne.RabbitDescription";
        private static Harmony harmony;

        internal static void Install()
        {
            if (harmony != null) return;
            MethodInfo update = typeof(UI_CostumePanel).GetMethod("UpdateData",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(CostumeEntity) }, null);
            FieldInfo effect = typeof(UI_CostumePanel).GetField("tooltipEffectText",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo close = typeof(UI_CostumePanel).GetMethod("OnClosed",
                BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (update == null || update.ReturnType != typeof(void) || effect == null ||
                effect.FieldType != typeof(TextMeshProUGUI) || close == null || close.ReturnType != typeof(void))
                throw new MissingMemberException("Native costume tooltip boundary changed.");

            var instance = new Harmony(Owner);
            try
            {
                instance.Patch(update, postfix: new HarmonyMethod(typeof(RabbitDescriptionHooks), nameof(AfterUpdate)));
                instance.Patch(close, postfix: new HarmonyMethod(typeof(RabbitDescriptionHooks), nameof(AfterClose)));
                harmony = instance;
            }
            catch { instance.UnpatchAll(Owner); throw; }
        }

        internal static void Uninstall()
        {
            if (harmony == null) return;
            harmony.UnpatchAll(Owner);
            harmony = null;
        }

        private static void AfterUpdate(UI_CostumePanel __instance, CostumeEntity costume) =>
            RabbitDescriptionFeature.AfterUpdate(__instance, costume);

        private static void AfterClose(UI_CostumePanel __instance) =>
            RabbitDescriptionFeature.AfterClose(__instance);
    }
}
