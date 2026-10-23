using System.Reflection;
using HarmonyLib;

internal static class GameProjectileRatePanelTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(panel, name)).ToList();
        bool Call(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        bool Field(CodeInstruction i, string name) => i.operand is FieldInfo f && f.Name == name;
        void Require(bool ok, string why) { if (!ok) throw new Exception("Projectile rate panel: " + why); }
        var build = Code("BuildProjectileEditor");
        Require(build.Any(i => i.LoadsConstant("Limit secondary spawn rate")) && build.Any(i => i.LoadsConstant("/one projectiles rate")) &&
            build.Any(i => i.LoadsConstant("Creations / player / s")) && build.Any(i => i.LoadsConstant("Creations / source / s")) &&
            build.Any(i => i.LoadsConstant("Reset spawn rates")), "Projectiles exposes independent rate toggle, values and reset");
        foreach (string field in new[] { "Total", "Source" })
        {
            string lower = field.ToLowerInvariant();
            var apply = Code("ApplyProjectileRate" + field);
            Require(apply.Any(i => i.LoadsConstant("/one projectiles rate " + lower + " ")) &&
                apply.Any(i => Call(i, "TMP_InputField", "get_text")) && apply.Any(i => Call(i, "SettingsPanel", "Execute")),
                "Rate " + lower + " passes typed input through shared commands");
            Require(Code("RefreshProjectileEditor").Any(i => Field(i, "projectileRate" + field + "Draft")) &&
                Code("BuildPage").Any(i => Field(i, "projectileRate" + field + "Draft")) &&
                Code("ClearProjectileRate" + field).Any(i => Field(i, "projectileRate" + field + "Draft")),
                "Rate drafts survive refresh and clear on page/session changes");
            string sibling = field == "Total" ? "Source" : "Total";
            Require(apply.Any(i => Call(i, "SettingsPanel", "ClearProjectileRate" + field)) &&
                !Code("ClearProjectileRate" + field).Any(i => Field(i, "projectileRate" + sibling + "Draft")), "Applying one rate preserves the sibling draft");
        }
        Require(Code("ClearProjectileInputs").Any(i => Call(i, "SettingsPanel", "ClearProjectileRates")), "Session cleanup clears both rate drafts");
        Require(Code("ResetProjectileRates").Any(i => i.LoadsConstant("/one projectiles rate reset")), "Rate reset uses its scoped command");
        Require(Code("ProjectileValues").Any(i => i.LoadsConstant(
            "Independent rate budget: stores 1 second and refills continuously. Includes supported secondary effects beyond active caps; denied attempts are skipped.")),
            "Help explains separate scope, continuous refill and consumed denials");
        Console.WriteLine("Verified projectile rate panel commands, draft lifecycle and independent-budget explanation (not live Unity rendering).");
    }
}
