using System.Reflection;
using HarmonyLib;

internal static class GameDeathmatchTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        List<CodeInstruction> Code(Assembly assembly, string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(assembly.GetType(type, true)!, method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string name) => code.Any(i => i.operand is MethodInfo m && m.Name == name);
        void Require(bool value, string message) { if (!value) throw new Exception("Deathmatch contract: " + message); }
        AccessTools.Method(addon.GetType("SephiriaOne.DeathmatchFeature", true)!, "Validate").Invoke(null, null);
        Require(Calls(Code(game, "DungeonManager", "Chat"), "RpcChat") && Calls(Code(game, "DungeonManager", "RpcChat"), "SendRPCInternal"), "host native chat broadcasts to stock guests");
        Require(Calls(Code(game, "DungeonManager", "UserCode_RpcChat__PlayerAvatar__String__String"), "CreateChatBubble"), "avatar chat renders native overhead bubble");
        Require(Calls(Code(game, "PlayerAvatar", "CreateChatBubble"), "Destroy") && Calls(Code(game, "PlayerAvatar", "CreateChatBubble"), "SetText"), "each countdown update replaces the previous bubble");
        foreach (var pair in new[] { ("PlayerAvatar", "CreateChatBubble"), ("UI_ChatBubble", "SetText"), ("UI_ChatBubble", "Update") })
            Require(!Code(game, pair.Item1, pair.Item2).Any(i => i.operand is FieldInfo f && (f.Name == "IsDead" || f.Name == "hiddenType")), "dead/hidden body does not suppress native bubble: " + pair);
        Require(Calls(Code(addon, "SephiriaOne.SessionSettingsController", "LateUpdate"), "Tick"), "existing host controller drives temporary match timers");
        Require(Calls(Code(addon, "SephiriaOne.FriendlyFireRuntime", "AfterDeath"), "Died") && Calls(Code(addon, "SephiriaOne.FriendlyFireRuntime", "BeforeRevive"), "Reviving"), "shared death/revival callbacks own match life timers");
        Require(Calls(Code(addon, "SephiriaOne.FriendlyFireRuntime", "BeforeGameOverCheck"), "ProtectDeath"), "only scoped match deaths may bypass game-over settlement");
        var unload = Code(addon, "SephiriaOne.Entry", "OnModUnloaded");
        int Shutdown(string type) => unload.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == "Shutdown");
        Require(Shutdown("DeathmatchFeature") >= 0 && Shutdown("DeathmatchFeature") < Shutdown("FriendlyFireFeature") &&
            Shutdown("DeathmatchFeature") < Shutdown("ReviveAllFeature"), "same-scope match recovery precedes native hook teardown");
        Require(Calls(Code(addon, "SephiriaOne.DeathmatchFeature", "Shutdown"), "get_RecoveryPending"), "callback-owned recovery blocks premature hook teardown");
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        Require(Code(addon, "SephiriaOne.SettingsPanel", "BuildDeathmatchEditor").Any(i =>
            i.opcode == System.Reflection.Emit.OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "deathmatchStart"),
            "dedicated tab retains its start control independently of its localized caption");
        foreach (string command in new[] { "/one deathmatch start", "/one deathmatch stop", "/one deathmatch duration " })
            Require(panel.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Any(m => m.GetMethodBody() != null &&
                PatchProcessor.GetOriginalInstructions(m).Any(i => Equals(i.operand, command))), "UI reuses shared command: " + command);
        Console.WriteLine("Verified native host chat/bubble replacement on dead avatars, shared match death/revival hooks, controller and UI command paths (not live multiplayer).");
    }
}
