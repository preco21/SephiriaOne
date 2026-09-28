using System.Reflection;
using HarmonyLib;

internal static class GamePanelCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        var ui = game.GetType("UIBase", true)!;
        var root = game.GetType("UIRoot", true)!;
        var open = Required(ui, "Open");
        var close = Required(ui, "Close");
        var add = Required(root, "AddControl");
        var remove = Required(root, "RemoveControl");
        if (!Calls(open, add) || !Calls(close, remove) || !close.IsVirtual ||
            !Calls(Required(ui, "CloseFromEsc"), close))
            throw new Exception("Native panel control-stack lifetime changed.");
        if (Required(ui, "SetRoot").GetParameters().Single().ParameterType != root ||
            AccessTools.Field(ui, "hasControl")?.FieldType != typeof(bool) ||
            AccessTools.Field(ui, "isPlayerUITHing")?.FieldType != typeof(bool) ||
            AccessTools.Property(ui, "ParentRoot")?.PropertyType != root)
            throw new Exception("Dynamic panel root/control API changed.");
        var manager = game.GetType("UIManager", true)!;
        var stackType = typeof(List<>).MakeGenericType(typeof(List<>).MakeGenericType(ui));
        if (AccessTools.Property(manager, "AllControlStack")?.PropertyType != stackType)
            throw new Exception("Native control-stack membership API changed.");
        foreach (string method in new[] { "OnControlAdded", "OnControlRemoved" })
        {
            var instructions = PatchProcessor.GetOriginalInstructions(Required(manager, method));
            if (!instructions.Any(i => i.operand is FieldInfo f && f.Name == "doingUIThingValue") ||
                !instructions.Any(i => i.operand is MethodInfo m && m.Name == "ControlUpdate"))
                throw new Exception("Native input-stack accounting changed: " + method);
        }
        var nativeCancel = Required(game.GetType("UIInputModule", true)!, "Update");
        if (!Calls(nativeCancel, Required(ui, "CloseFromEsc")))
            throw new Exception("Native cancel no longer uses panel closing.");

        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        if (panel.BaseType != ui) throw new Exception("Addon panel must participate in native UIBase lifecycle.");
        foreach (string method in new[] { "Show", "Close" })
        {
            if (!PatchProcessor.GetOriginalInstructions(Required(panel, method)).Any(i =>
                i.operand is MethodInfo m && m.DeclaringType?.Name == "PanelControlLifetime`1" &&
                m.Name == (method == "Show" ? "Open" : "Close")))
                throw new Exception("Panel must use the tested native membership protocol: " + method);
        }
        var dispose = Required(addon.GetType("SephiriaOne.SettingsPanelController", true)!, "TryDisposePanel");
        if (!Calls(dispose, Required(panel, "get_HasControlRegistration")))
            throw new Exception("Panel disposal must retain unresolved native registrations.");
        var shared = addon.GetType("SephiriaOne.SettingsActions", true)!;
        var execute = Required(shared, "Execute");
        if (!Calls(Required(panel, "Execute"), execute) ||
            !Calls(Required(addon.GetType("SephiriaOne.ModChatCommands", true)!, "OnSubmitted"), execute))
            throw new Exception("Chat and UI must use the same validated settings actions.");
        var chatIl = PatchProcessor.GetOriginalInstructions(Required(addon.GetType("SephiriaOne.ModChatCommands", true)!, "OnSubmitted"));
        int dispatch = chatIl.FindIndex(i => Equals(i.operand, execute));
        int consume = chatIl.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_text");
        if (consume < 0 || consume >= dispatch)
            throw new Exception("Local command text must be consumed before actions execute.");
        var refreshCalls = PatchProcessor.GetOriginalInstructions(Required(panel, "Refresh"));
        if (refreshCalls.Any(i => Equals(i.operand, execute)))
            throw new Exception("Refreshing the panel must not dispatch settings changes.");
        var snapshot = Required(panel, "ReadCurrentSnapshot");
        var snapshotInstructions = PatchProcessor.GetOriginalInstructions(snapshot);
        if (!Calls(snapshot, Required(addon.GetType("SephiriaOne.SessionSettings", true)!, "ReadSnapshot")) ||
            !snapshotInstructions.Any(i => i.operand is FieldInfo f && f.DeclaringType == panel && f.Name == "page") ||
            !snapshotInstructions.Any(i => i.LoadsConstant(5)) ||
            !snapshotInstructions.Any(i => i.opcode == System.Reflection.Emit.OpCodes.Ceq))
            throw new Exception("Only the Status page should request full formatted diagnostics.");
        foreach (string method in new[] { "Show", "SelectPage", "MoveSelection", "Execute", "RefreshSaved" })
            if (!Calls(Required(panel, method), snapshot))
                throw new Exception("Panel snapshot detail selection is bypassed by " + method);
        if (!Calls(Required(addon.GetType("SephiriaOne.SettingsPanelController", true)!, "Update"), snapshot))
            throw new Exception("Polling must use the panel's current snapshot detail selection.");
        var entryUnload = PatchProcessor.GetOriginalInstructions(Required(addon.GetType("SephiriaOne.Entry", true)!, "OnModUnloaded"));
        int cleanup = entryUnload.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ChoiceFeature" && m.Name == "Shutdown");
        int disable = entryUnload.FindIndex(i => i.operand is MethodInfo m && m.Name == "set_enabled");
        if (cleanup < 0 || disable < cleanup)
            throw new Exception("Faulted contribution cleanup must retain recovery controls.");
        Console.WriteLine("Verified native panel stack, cancel, shared chat/UI dispatch and cleanup ordering (not live rendering/input).");
    }

    private static MethodInfo Required(Type type, string name) => AccessTools.DeclaredMethod(type, name)
        ?? throw new Exception("Required UI method missing: " + type.Name + "." + name);
    private static bool Calls(MethodInfo caller, MethodInfo target) =>
        PatchProcessor.GetOriginalInstructions(caller).Any(i => Equals(i.operand, target));
}
