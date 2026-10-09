using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameReviveAllTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Calls(IEnumerable<CodeInstruction> code, string method) => code.Any(i => i.operand is MethodInfo m && m.Name == method);
        void Require(bool value, string why) { if (!value) throw new Exception("Revive-all contract: " + why); }
        var revive = Code("UnitAvatar", "Revive");
        foreach (string call in new[] { "set_NetworkIsDead", "set_Networkhp", "ClearDamageRestoreHp", "StartReviveInvulnerable", "TakeRemoteInventory", "RpcRevive" })
            Require(Calls(revive, call), "native revival retains " + call);
        Require(revive.Any(i => i.operand is FieldInfo f && f.Name == "OnRevive"), "server revival callbacks run");
        var recoveryHooks = addon.GetType("SephiriaOne.ReviveAllHooks", true)!;
        var wrapped = ((IEnumerable<CodeInstruction>)AccessTools.Method(recoveryHooks, "Rewrite").Invoke(null, new object[] { revive })!).ToList();
        Require(wrapped.Count == revive.Count + 2 && wrapped.Count(i => i.operand is MethodInfo m && m.DeclaringType == recoveryHooks) == 2 &&
            Calls(wrapped, "RpcRevive") && Calls(wrapped, "StartReviveInvulnerable") && Calls(wrapped, "TakeRemoteInventory"),
            "exactly two action-scoped event wrappers preserve the native recovery tail");
        var changedRevive = revive.Select(i => new CodeInstruction(i)).ToList();
        changedRevive.RemoveAt(changedRevive.FindIndex(i => i.operand is MethodInfo m && m.Name == "Invoke"));
        bool callbacksRejected = false;
        try { AccessTools.Method(recoveryHooks, "Rewrite").Invoke(null, new object[] { changedRevive }); }
        catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { callbacksRejected = true; }
        Require(callbacksRejected, "missing native callback rejects recovery installation");
        Require(Calls(Code("UnitAvatar", "RpcRevive"), "SendRPCInternal"), "stock revival RPC is broadcast");
        var remote = Code("UnitAvatar", "UserCode_RpcRevive");
        Require(remote.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "hiddenType") &&
            remote.Any(i => i.operand is FieldInfo f && f.Name == "OnReviveClientside"), "guest revival restores visible body and invokes observers");
        Require(Calls(Code("GameCamera", "OnRevive"), "ResetAltTarget"), "native camera exits alternate/spectator target");
        var gameOver = Code("PlayerSpawner", "HandleDieServerside");
        var hooks = addon.GetType("SephiriaOne.FriendlyFireHooks", true)!;
        Require((bool)AccessTools.Method(hooks, "ValidateGameOverCheck").Invoke(null, new object[] { gameOver })!, "native all-dead callback is the audited game-over-only boundary");
        var changed = gameOver.Select(i => new CodeInstruction(i)).ToList();
        changed.RemoveAt(changed.FindIndex(i => i.operand is MethodInfo m && m.Name == "RpcGameOver"));
        Require(!(bool)AccessTools.Method(hooks, "ValidateGameOverCheck").Invoke(null, new object[] { changed })!, "changed game-over path is rejected");
        var settlement = Code("UI_GameOverLabel", "OnOpened");
        Require(settlement.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "enableSave") && Calls(settlement, "DeleteFile"),
            "native settlement disables and deletes the run save; no recovery rollback is possible");
        Require(Calls(Code("PlayerSpawner", "ClientGameOver"), "ResetSession") && Calls(Code("QuestController", "Awake"), "add_OnGameOverServerside"),
            "game over settles combat and quest lifecycle");
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var panelBuild = PatchProcessor.GetOriginalInstructions(AccessTools.Method(panel, "BuildCombatEditor"));
        Require(panelBuild.Any(i => Equals(i.operand, "Revive all players")), "Combat tab builds recovery button");
        Require(panel.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Any(m => m.GetMethodBody() != null &&
            PatchProcessor.GetOriginalInstructions(m).Any(i => Equals(i.operand, "/one reviveall"))), "button dispatches shared command");
        var execute = PatchProcessor.GetOriginalInstructions(AccessTools.Method(addon.GetType("SephiriaOne.SettingsActions", true)!, "Execute"));
        Require(execute.Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ReviveAllAction" && m.Name == "TryExecute"), "chat and UI share recovery service");
        Console.WriteLine("Verified native full revival, guest rendering/camera RPC, scoped friendly-fire game-over guard, terminal settlement and shared recovery button/command (not live multiplayer).");
    }
}
