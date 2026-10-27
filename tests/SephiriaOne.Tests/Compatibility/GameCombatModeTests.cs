using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameCombatModeTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        void Check(bool value, string why) { if (!value) throw new Exception("Combat Mode contract: " + why); }
        var unit = game.GetType("UnitAvatar", true)!;
        var player = game.GetType("PlayerAvatar", true)!;
        var hooks = addon.GetType("SephiriaOne.CombatModeHooks", true)!;
        Check((bool)AccessTools.Method(hooks, "ValidatePresentation").Invoke(null, null)!, "native presentation gates pass installer contract");
        var counter = AccessTools.Field(unit, "isInBattle");
        var setter = AccessTools.PropertySetter(unit, "NetworkisInBattle");
        Check(counter.FieldType == typeof(sbyte), "signed native battle reference count");
        var sync = PatchProcessor.GetOriginalInstructions(setter).ToList();
        Check(sync.Any(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, counter)) &&
            sync.Any(i => i.LoadsConstant(67108864L)) && sync.Any(i => i.operand is MethodInfo m && m.Name == "GeneratedSyncVarSetter"),
            "native SyncVar setter preserves replication dirty bit");
        foreach (string op in new[] { "Start", "Stop" })
        {
            var method = AccessTools.DeclaredMethod(unit, "Local" + op + "Battle", Type.EmptyTypes);
            var code = PatchProcessor.GetOriginalInstructions(method).ToList();
            bool Valid(List<CodeInstruction> c) => (bool)AccessTools.Method(hooks, "ValidateBattle").Invoke(null, new object[] { c, op == "Start" })!;
            Check(Valid(code), "native battle " + op + " overlay boundary");
            var altered = code.Select(i => new CodeInstruction(i)).ToList();
            int at = altered.FindIndex(i => i.Calls(setter)); altered[at - 2] = new CodeInstruction(OpCodes.Nop);
            Check(!Valid(altered), "changed counter arithmetic fails closed");
            altered = code.Select(i => new CodeInstruction(i)).ToList(); altered.Add(new CodeInstruction(code[at]));
            Check(!Valid(altered), "duplicate counter writer fails closed");
        }
        var inBattle = AccessTools.PropertyGetter(unit, "IsInBattle");
        foreach (string handler in new[] { "HandleOnOpenCharacterStatusPanel", "HandleOpenStatsPanel", "HandleOpenPassivePanel", "HandleOpenLevelUpPanel", "HandleOnOpenMapPanel" })
            Check(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType("PlayerInputController", true), handler)).Any(i => i.Calls(inBattle)), "stock guest opening gate: " + handler);
        foreach (string panel in new[] { "UI_MapPanel", "UI_NewWorldMapPanel" })
        {
            var code = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(game.GetType(panel, true), "Update")).ToList();
            Check(code.Any(i => i.Calls(inBattle)) && code.Any(i => i.operand is MethodInfo m && m.Name == "Close"), "stock open map closes: " + panel);
        }
        Check(AccessTools.DeclaredMethod(player, "MoveFloorViaWorldmap", new[] { typeof(string), typeof(bool), typeof(string) }) != null, "host worldmap request boundary");
        var room = game.GetType("UI_Map_Room", true)!;
        var teleport = PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(room, "TeleportToRoom")).ToList();
        Check(teleport.Any(i => i.Calls(inBattle)), "stock same-floor teleport gate");
        Check(PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(addon.GetType("SephiriaOne.SettingsPanel", true), "BuildCombatEditor")).Any(i =>
            i.opcode == OpCodes.Ldstr && Equals(i.operand, "/one friendlyfire combat-mode")), "UI uses shared command");
        Console.WriteLine("Verified native Combat Mode SyncVar, menu/map consumers, battle boundaries, fail-closed mutations and UI command.");
    }
}
