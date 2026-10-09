using System.Reflection;
using HarmonyLib;

internal static class GameHotkeyCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        foreach (var field in new[] { ("UI_OptionsPanel", "tab"), ("UI_OptionsPanel", "actions"), ("UI_Tab", "tabContents"), ("OptionsBinding", "actionAsset") })
            if (AccessTools.Field(game.GetType(field.Item1, true), field.Item2) == null) throw new Exception("Missing native shortcut contract: " + field);
        if (AccessTools.Property(game.GetType("RebindActionUI", true), "ongoingRebind") == null ||
            AccessTools.Property(game.GetType("ControlsChangeHandler", true), "PlayerInput") == null)
            throw new Exception("Native capture/conflict contracts changed.");
        var controller = addon.GetType("SephiriaOne.HotkeyController", true)!;
        var tryKey = AccessTools.Method(controller, "TryKey");
        foreach (string key in new[] { "Escape", "Enter", "LeftShift", "Tab", "UpArrow", "Unknown", "999", "1" })
            if ((bool)tryKey.Invoke(null, new object[] { key, null! })!) throw new Exception("Unsafe shortcut accepted: " + key);
        if (!(bool)tryKey.Invoke(null, new object[] { "F9", null! })!) throw new Exception("Valid invariant key rejected.");
        var adapter = addon.GetType("SephiriaOne.NativeHotkeyOptions", true)!;
        var attach = PatchProcessor.GetOriginalInstructions(AccessTools.Method(adapter, "Attach"));
        if (!attach.Any(i => Equals(i.operand, "Tab-Controls-Keyboard")) ||
            !attach.Any(i => Equals(i.operand, "SephiriaOne.Hotkey")) ||
            !attach.Any(i => i.operand is MethodInfo m && m.Name == "get_content"))
            throw new Exception("Addon entry must use the audited keyboard ScrollRect content.");
        var dispose = PatchProcessor.GetOriginalInstructions(AccessTools.Method(adapter, "Dispose"));
        if (!dispose.Any(i => i.operand is FieldInfo f && f.Name == "owned") ||
            dispose.Any(i => i.operand is MethodInfo m && m.Name == "GetComponentsInChildren"))
            throw new Exception("Options cleanup must remove only its owned identity.");
        if (!PatchProcessor.GetOriginalInstructions(AccessTools.Method(controller, "OtherMenu")).Any(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "HotkeyInputOwnership" && m.Name == "IsBlocked"))
            throw new Exception("Native shortcut ownership must use tested top-control policy.");
        var capture = PatchProcessor.GetOriginalInstructions(AccessTools.Method(controller, "Capture"));
        var tick = PatchProcessor.GetOriginalInstructions(AccessTools.Method(controller, "Tick"));
        if (!capture.Any(i => i.operand is MethodInfo m && m.Name == "get_allKeys") ||
            tick.Any(i => i.operand is MethodInfo m && m.Name == "get_allKeys"))
            throw new Exception("Keyboard scanning belongs only to active capture.");
        if (!tick.Any(i => i.operand is MethodInfo m && m.Name == "TryToggle"))
            throw new Exception("Shortcut must use the existing window toggle.");
        foreach (var type in new[] { controller, adapter })
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                foreach (var instruction in PatchProcessor.GetOriginalInstructions(method))
                    if (instruction.operand is MethodInfo called &&
                        (called.Name.Contains("BindingOverride") || called.Name.Contains("InteractiveRebinding") || called.Name == "RemoveAllListeners"))
                        throw new Exception("Shortcut must not mutate native bindings/listeners: " + called.Name);
        Console.WriteLine("Verified native shortcut adapter layout, identity cleanup, capture-only key scan, window toggle and absence of native binding mutation (not live Unity input).");
    }
}
