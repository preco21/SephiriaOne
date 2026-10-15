using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameLobbyRegenTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var hooks = addon.GetType("SephiriaOne.LobbyRegenHooks", true)!;
        void Check(bool value, string why) { if (!value) throw new Exception("Lobby recovery: " + why); }
        foreach (string kind in new[] { "Player", "Unit" })
        {
            var method = AccessTools.DeclaredMethod(game.GetType(kind + "Avatar", true), "Update");
            var code = PatchProcessor.GetOriginalInstructions(method).ToList();
            bool Valid(List<CodeInstruction> c) => (bool)AccessTools.Method(hooks, "Validate" + kind).Invoke(null, new object[] { c })!;
            Check(Valid(code), "Installed " + kind + " Update has the expected recovery sites");
            var rewritten = ((IEnumerable<CodeInstruction>)AccessTools.Method(hooks, "Rewrite" + kind).Invoke(null,
                new object[] { code.Select(i => new CodeInstruction(i)).ToList() })!).ToList();
            var changed = code.Zip(rewritten).Where(pair => pair.First.opcode != pair.Second.opcode || !Equals(pair.First.operand, pair.Second.operand)).ToArray();
            Check(changed.Length == (kind == "Player" ? 1 : 2), "Only intended healing calls are replaced in " + kind);
            Check(changed.All(p => p.Second.operand is MethodInfo m && m.DeclaringType?.Name == "LobbyRegenRuntime"), "Replacement wrappers preserve the rest of Update");
            foreach (var pair in changed)
            {
                var missing = code.Select(i => new CodeInstruction(i)).ToList();
                missing.RemoveAt(code.IndexOf(pair.First));
                Check(!Valid(missing), "Missing recovery call fails closed");
                var duplicate = code.Select(i => new CodeInstruction(i)).ToList(); duplicate.Add(new CodeInstruction(pair.First));
                Check(!Valid(duplicate), "Ambiguous recovery call fails closed");
                var wrongAmount = code.Select(i => new CodeInstruction(i)).ToList();
                int constant = code.IndexOf(pair.First) - (kind == "Player" ? 3 : 1);
                wrongAmount[constant] = new CodeInstruction(OpCodes.Nop);
                Check(!Valid(wrongAmount), "Changed recovery amount fails closed");
            }
        }
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        Check(PatchProcessor.GetOriginalInstructions(AccessTools.Method(panel, "BuildFriendlyDamageEditor")).Any(i =>
            i.opcode == OpCodes.Ldstr && Equals(i.operand, "/one friendlyfire block-lobby-regen")), "UI uses the shared command");
        Console.WriteLine("Verified installed waiting-room/passive HP/MP recovery sites, targeted rewrites, fail-closed mutations and shared UI command.");
    }
}
