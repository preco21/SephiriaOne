using System.Reflection;
using HarmonyLib;

internal static class GameUpdateCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        MethodInfo Method(Type type, string name) => AccessTools.DeclaredMethod(type, name) ?? throw new Exception("Missing update contract: " + name);
        List<CodeInstruction> Code(Type type, string name) => PatchProcessor.GetOriginalInstructions(Method(type, name));
        bool Calls(IEnumerable<CodeInstruction> code, string owner, string name) =>
            code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name);
        void Require(bool valid, string message) { if (!valid) throw new Exception(message); }
        var loader = game.GetType("AddOnLoader", true)!;
        Require(Calls(Code(loader, "LoadAssembly"), "Assembly", "LoadFrom"), "SDK must still load the explicitly selected DLL");
        Require(Code(loader, "ResolveDllPath").Any(i => i.operand is FieldInfo f && f.Name == "dllFile") &&
            Calls(Code(loader, "ResolveDllPath"), "Path", "Combine"), "Version-named DLL selection must use metadata.dllFile");
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var click = panel.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Select(method =>
            (Method: method, Code: PatchProcessor.GetOriginalInstructions(method))).FirstOrDefault(pair =>
                pair.Code.Any(i => Equals(i.operand, "/one update install")));
        Require(click.Method != null && Calls(click.Code, "SettingsPanel", "Execute"), "Update button must execute the explicit update command");
        Require(Code(panel, "BuildUpdatesEditor").Any(i => i.operand is MethodInfo m && m == click.Method), "Update callback must be wired into the visible editor");
        var actions = addon.GetType("SephiriaOne.SettingsActions", true)!;
        Require(Calls(Code(actions, "Execute"), "UpdateCommand", "Execute"), "Chat/UI dispatch must reach the same update handler");
        var update = addon.GetType("SephiriaOne.UpdateController", true)!;
        Require(Calls(Code(update, "Update"), "GameLogWriter", "WriteLog"), "Update notices must use the native local message surface");
        foreach (var type in addon.GetTypes().Where(t => t.Name.StartsWith("Update") || t.Name == "GitHubReleaseClient"))
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (method.GetMethodBody() == null) continue;
                var code = PatchProcessor.GetOriginalInstructions(method);
                Require(!Calls(code, "AddOnLoader", "LoadAll") && !Calls(code, "Assembly", "LoadFrom") &&
                    !code.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "SessionSettings"),
                    "Updater must not hot-load new code or replay session settings");
            }
        Require(Calls(Code(update, "OnDisable"), "UpdateFeature", "Stop"), "Updater must cancel during controller disposal");
        Console.WriteLine("Verified native versioned DLL selection, command/button dispatch, local notices and updater isolation (not live Unity networking/restart).");
    }
}
