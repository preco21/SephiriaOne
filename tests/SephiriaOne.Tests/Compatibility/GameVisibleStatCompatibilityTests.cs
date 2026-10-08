using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SephiriaOne;

internal static class GameVisibleStatCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Require(bool value, string why) { if (!value) throw new Exception("Visible stat native contract: " + why); }
        var hook = game.GetType("AvatarStatsHooker", true)!;
        var native = PatchProcessor.GetOriginalInstructions(AccessTools.Method(hook, "HookStat"));
        var enumType = game.GetType("ECustomStat", true)!;
        var unit = game.GetType("UnitAvatar", true)!;
        var convertedKeys = PatchProcessor.GetOriginalInstructions(AccessTools.Method(unit, "ElementalIndexOf"))
            .Where(i => i.opcode == OpCodes.Ldstr).Select(i => (string)i.operand).ToHashSet();
        Require(convertedKeys.Count == 4 && !StatCatalog.All.Any(s => convertedKeys.Contains(s.Key)),
            "Independent stat planner must exclude conversion-dependent elemental keys");
        var read = PatchProcessor.GetOriginalInstructions(AccessTools.Method(unit, "GetCustomStatUnsafe"));
        foreach (string method in new[] { "ElementalIndexOf", "GetConvertedElementalDamage", "GetRawStatUnsafe" })
            Require(read.Any(i => i.operand is MethodInfo m && m.DeclaringType == unit && m.Name == method), "Native raw/converted routing changed");
        foreach (var stat in StatCatalog.All)
        {
            int comparison = native.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, stat.MenuId));
            if (stat.Name == "debuffdamage" || stat.Name == "debuffduration")
            {
                Require(comparison < 0 && stat.Key == stat.MenuId && stat.Offset == 0 && stat.Scale == 1, "Debuff menu uses raw-key default path");
                Require(native.Where((i, n) => n > 0 && native[n - 1].opcode == OpCodes.Ldarg_1 &&
                    i.operand is MethodInfo m && m.Name == "GetCustomStatUnsafe").Any(), "Default path still reads requested raw key");
                continue;
            }
            Require(comparison >= 0, "Menu hook removed: " + stat.MenuId);
            int branch = comparison + 2;
            Require(native[branch].opcode == OpCodes.Brtrue || native[branch].opcode == OpCodes.Brtrue_S, "Menu dispatcher changed " + stat.MenuId);
            var label = (Label)native[branch].operand;
            int first = native.FindIndex(i => i.labels.Contains(label));
            int last = native.FindIndex(first, i => i.opcode == OpCodes.Ret);
            var body = native.Skip(first).Take(last - first + 1).ToList();
            string? enumName = Enum.GetNames(enumType).SingleOrDefault(n => n.ToUpperInvariant() == stat.Key);
            bool readsKey = body.Where((i, n) => n > 0 && i.operand is MethodInfo m &&
                ((enumName != null && m.Name == "GetCustomStat" && body[n - 1].LoadsConstant(Convert.ToInt32(Enum.Parse(enumType, enumName)))) ||
                    (m.Name == "GetCustomStatUnsafe" && Equals(body[n - 1].operand, stat.Key)))).Any();
            Require(readsKey, "Menu no longer reads catalog key " + stat.Name);
            if (stat.Offset != 0) Require(body.Any(i => i.LoadsConstant(stat.Offset)) && body.Any(i => i.opcode == OpCodes.Add), "Menu display offset " + stat.Name);
            if (stat.Scale != 1) Require(native.Any(i => i.opcode == OpCodes.Ldc_R4 && Equals(i.operand, (float)stat.Scale)), "Display divisor " + stat.Name);
        }
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        Require(PatchProcessor.GetOriginalInstructions(AccessTools.Method(panel, "UpdateSelection")).Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "StatDefinition" && m.Name == "get_Label"), "Panel must use catalog label");
        Console.WriteLine("Verified supported stat keys and displayed offsets against installed native C-menu readers (asset eligibility audit documented separately; not live UI).");
    }
}
