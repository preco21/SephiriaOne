using System.Reflection;
using HarmonyLib;

internal static class GamePanelCompatibilityTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        GameHotkeyCompatibilityTests.Run(game, addon);
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
        var contentForPage = Required(panel, "ContentForPage");
        string[] expected = { "Stats", "None", "Choices", "Resources", "Presets", "All", "None", "None", "None", "None", "None", "None", "None", "None", "All" };
        for (int page = 0; page < expected.Length; page++)
            if (contentForPage.Invoke(null, new object[] { page })?.ToString() != expected[page])
                throw new Exception("Panel snapshot must include every displayed section: page " + page);
        if (!Calls(Required(panel, "ReadCurrentSnapshot"), contentForPage) ||
            !PatchProcessor.GetOriginalInstructions(Required(panel, "ReadCurrentSnapshot")).Any(i =>
                i.operand is MethodInfo m && m.Name == "ReadSnapshot" &&
                m.GetParameters()[0].ParameterType.Name == "SnapshotContent"))
            throw new Exception("Panel must request only its displayed snapshot sections.");
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
        if (!Calls(snapshot, contentForPage) ||
            !snapshotInstructions.Any(i => i.operand is FieldInfo f && f.DeclaringType == panel && f.Name == "page"))
            throw new Exception("Snapshot content must follow the current panel page.");
        foreach (string method in new[] { "Show", "SelectPage", "MoveSelection", "Execute", "RefreshSaved" })
            if (!Calls(Required(panel, method), snapshot))
                throw new Exception("Panel snapshot detail selection is bypassed by " + method);
        if (!Calls(Required(addon.GetType("SephiriaOne.SettingsPanelController", true)!, "Update"), snapshot))
            throw new Exception("Polling must use the panel's current snapshot detail selection.");
        var entryUnload = PatchProcessor.GetOriginalInstructions(Required(addon.GetType("SephiriaOne.Entry", true)!, "OnModUnloaded"));
        int cleanup = entryUnload.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "ChoiceFeature" && m.Name == "Shutdown");
        // Local updater disposal may precede gameplay cleanup. Recovery depends
        // specifically on retaining the panel/chat/session/name controllers.
        var controls = new[] { "settingsPanel", "chatCommands", "sessionSettings", "nameColor" };
        if (cleanup < 0 || controls.Any(name => entryUnload.FindIndex(i =>
            i.operand is FieldInfo f && f.DeclaringType?.Name == "Entry" && f.Name == name) < cleanup))
            throw new Exception("Faulted contribution cleanup must retain recovery controls.");
        var controller = addon.GetType("SephiriaOne.SettingsPanelController", true)!;
        var bindingLifetime = addon.GetType("SephiriaOne.PanelBindingLifetime", true)!;
        if (!Calls(Required(controller, "Update"), Required(bindingLifetime, "TryRebind")))
            throw new Exception("Native binding updates must use the tested manager/launcher lifetime protocol.");
        var launcherRelease = PatchProcessor.GetOriginalInstructions(Required(controller, "ReleaseLauncher"));
        if (launcherRelease.Any(i => i.operand is FieldInfo f && f.DeclaringType == controller &&
            (f.Name == "panel" || f.Name == "root" || f.Name == "font")) ||
            launcherRelease.Any(i => i.operand is MethodInfo m && m.Name == "DisposePanel"))
            throw new Exception("Optional launcher replacement must preserve window/root/font lifetime.");
        if (!Calls(Required(controller, "Release"), Required(controller, "DisposePanel")) ||
            !Calls(Required(controller, "Release"), Required(controller, "ReleaseLauncher")))
            throw new Exception("Manager teardown must still release both window and launcher.");
        var togglePanel = Required(controller, "TryToggle");
        if (!togglePanel.IsPublic || !togglePanel.IsStatic || togglePanel.ReturnType != typeof(bool) ||
            togglePanel.GetParameters().Single().ParameterType != typeof(string).MakeByRefType() ||
            !Calls(togglePanel, close) ||
            !Calls(togglePanel, Required(controller, "TryOpen")))
            throw new Exception("Hotkey adapter requires the owned-window toggle API.");
        if (PatchProcessor.GetOriginalInstructions(togglePanel).Any(i => i.operand is MethodInfo m && m.Name == "CloseAllControl"))
            throw new Exception("Window toggle must never close unrelated panels.");
        var rootLookup = PatchProcessor.GetOriginalInstructions(Required(controller, "ResolveRoot"));
        if (!rootLookup.Any(i => i.operand is FieldInfo f && f.DeclaringType == manager && f.Name == "uiRoots"))
            throw new Exception("Window must use registered native roots.");
        if (PatchProcessor.GetOriginalInstructions(Required(controller, "TryOpen")).Any(i =>
            i.operand is FieldInfo f && f.DeclaringType == controller && f.Name == "pause"))
            throw new Exception("Direct opening must not require the optional pause launcher.");
        var drag = addon.GetType("SephiriaOne.PanelWindowDrag", true)!;
        foreach (string contract in new[] { "IBeginDragHandler", "IDragHandler" })
            if (!drag.GetInterfaces().Any(i => i.Name == contract)) throw new Exception("Title dragging requires " + contract);
        var checkbox = addon.GetType("SephiriaOne.PanelCheckbox", true)!;
        var observation = PatchProcessor.GetOriginalInstructions(Required(checkbox, "Refresh"));
        if (!observation.Any(i => i.operand is MethodInfo m && m.Name == "SetIsOnWithoutNotify") ||
            observation.Any(i => i.operand is MethodInfo m && m.Name == "Execute"))
            throw new Exception("Snapshot checkbox refresh must use no-notify observation.");
        var widgets = addon.GetType("SephiriaOne.PanelWidgets", true)!;
        if (Required(widgets, "Checkbox").ReturnType.Name != "Toggle" || Required(widgets, "Dropdown").ReturnType.Name != "TMP_Dropdown")
            throw new Exception("Window must expose real checkbox and stat selection widgets.");
        Console.WriteLine("Verified draggable-window interfaces, registered root lookup, owned toggle and no-notify checkbox/stat widgets (not live rendering/input).");
        Console.WriteLine("Verified native panel stack, cancel, shared chat/UI dispatch and cleanup ordering (not live rendering/input).");
    }

    private static MethodInfo Required(Type type, string name) => AccessTools.DeclaredMethod(type, name)
        ?? throw new Exception("Required UI method missing: " + type.Name + "." + name);
    private static bool Calls(MethodInfo caller, MethodInfo target) =>
        PatchProcessor.GetOriginalInstructions(caller).Any(i => Equals(i.operand, target));
}
