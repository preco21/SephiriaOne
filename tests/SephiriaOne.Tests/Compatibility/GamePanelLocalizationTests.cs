using System.Reflection;
using HarmonyLib;

internal static class GamePanelLocalizationTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var refreshLanguage = Required(panel, "RefreshLanguage");
        if (!Calls(Required(panel, "Refresh")).Contains(refreshLanguage))
            throw new Exception("Polling must refresh an existing panel after a language revision.");
        var visited = new HashSet<MethodInfo>();
        void Visit(MethodInfo method)
        {
            if (!visited.Add(method)) return;
            foreach (var call in Calls(method))
            {
                string owner = call.DeclaringType?.Name ?? "";
                if (owner == "SettingsActions" || owner.StartsWith("PanelControlLifetime") ||
                    owner == "UIBase" || owner == "SettingsPanelController" ||
                    (owner == "SessionSettings" && call.Name != "ReadSnapshot"))
                    throw new Exception("Language refresh must not replay settings or change native control ownership: " + call);
                if (call.DeclaringType?.Assembly == addon &&
                    (owner == "SettingsPanel" || owner == "PanelWidgets")) Visit(call);
            }
        }
        Visit(refreshLanguage);
        if (!visited.Contains(Required(panel, "BuildPage")))
            throw new Exception("Language refresh must rebuild the current page and discard stale drafts.");
        if (!Calls(Required(panel, "BuildPage")).Any(m => m.DeclaringType?.Name == "PanelDraft" && m.Name == "Clear"))
            throw new Exception("Language changes must clear unapplied action drafts.");

        var resolver = addon.GetType("SephiriaOne.PanelFontResolver", true)!;
        var resolve = Required(resolver, "Resolve");
        var instructions = PatchProcessor.GetOriginalInstructions(resolve);
        var nativeLocalization = game.GetType("LocalizationManager", true)!;
        if (!Calls(resolve).Any(m => m.DeclaringType == nativeLocalization && m.Name == "GetText") ||
            !instructions.Any(i => Equals(i.operand, "ko-KR")))
            throw new Exception("Korean panel font must resolve independently of the native current language.");
        if (Calls(resolve).Any(m => m.Name == "LoadLanguage" || m.Name == "ChangeFont" || m.Name.StartsWith("set_")))
            throw new Exception("Addon font resolution must not change native language/font settings.");
        Console.WriteLine("Verified UI language refresh is presentation-only, clears drafts and uses the native Korean font without changing game settings (not live rendering).");
    }

    private static MethodInfo Required(Type type, string name) => AccessTools.DeclaredMethod(type, name)
        ?? throw new Exception("Required localized UI method missing: " + type.Name + "." + name);

    private static IEnumerable<MethodInfo> Calls(MethodInfo method) =>
        PatchProcessor.GetOriginalInstructions(method)
            .Where(i => i.opcode == System.Reflection.Emit.OpCodes.Call || i.opcode == System.Reflection.Emit.OpCodes.Callvirt)
            .Select(i => i.operand).OfType<MethodInfo>();
}
