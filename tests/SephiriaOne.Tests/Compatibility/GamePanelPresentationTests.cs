using System.Reflection;
using HarmonyLib;

internal static class GamePanelPresentationTests
{
    internal static void Run(Assembly addon)
    {
        Type Type(string name) => addon.GetType("SephiriaOne." + name, true)!;
        List<CodeInstruction> Code(string type, string method) =>
            PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method));
        bool Calls(IEnumerable<CodeInstruction> body, string owner, string name) => body.Any(i =>
            i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name);
        void Check(bool value, string reason) { if (!value) throw new Exception("Panel presentation: " + reason); }

        var dropdown = addon.GetType("SephiriaOne.PanelDropdown");
        Check(dropdown?.BaseType?.Name == "TMP_Dropdown", "Addon dropdown must place its popup below the native cursor canvas");
        var popup = Code("PanelDropdown", "CreateDropdownList");
        Check(Calls(popup, "TMP_Dropdown", "CreateDropdownList") && Calls(popup, "Canvas", "set_sortingOrder") &&
            !Calls(popup, "Cursor", "set_visible"), "Preserve native popup behavior while fixing ordering, without forcing a second cursor");
        Check(Calls(Code("PanelWidgets", "AddScrollbar"), "ScrollRect", "set_verticalScrollbarVisibility"),
            "Hide the whole scrollbar rail when content fits");
        foreach (string method in new[] { "BuildPvpBalanceEditor", "BuildPvpDotEditor", "BuildParticipantsEditor" })
            Check(Calls(Code("SettingsPanel", method), "PanelWidgets", "ScrollSection"), "Padded sections share scroll layout: " + method);
        var section = Code("PanelWidgets", "ScrollSection");
        Check(Calls(section, "ScrollRect", "set_content") && Calls(section, "ScrollRect", "set_viewport") &&
            Calls(section, "PanelWidgets", "AddScrollbar"), "Shared padded section still has a masked viewport and native scrollbar");
        var projectiles = Code("SettingsPanel", "BuildProjectileEditor");
        int readout = projectiles.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.Name == "PanelWidgets" && m.Name == "Scroll");
        Check(readout >= 0 && projectiles.Take(readout).Any(i => i.LoadsConstant("Creations / source / s")),
            "Projectile readout is built below the spawn-rate controls, not behind them");
        // Rect(parent,x,y,width,height): inspect the four constant arguments to
        // the readout, ensuring the y=207,height=25 control row cannot overlap it.
        Check(readout >= 4 && projectiles[readout - 3].LoadsConstant(244f) && projectiles[readout - 1].LoadsConstant(122f),
            "Projectile readout starts after the final row and fits within the 366-unit page");

        Check(Type("PanelSurface").BaseType?.Name == "Image" && Type("PanelSectionIcon").BaseType?.Name == "MaskableGraphic",
            "Rounded graphics retain native tinting and clipping components");
        foreach (string name in new[] { "PanelSurface", "PanelSectionIcon", "PanelScrollArrow", "PanelNavigationFocus" })
            Check(AccessTools.DeclaredMethod(Type(name), "Update") == null && AccessTools.DeclaredMethod(Type(name), "LateUpdate") == null,
                name + " must not add per-frame work");
        foreach (string name in new[] { "scroll", "direction" })
            Check(AccessTools.Field(Type("PanelScrollArrow"), name).CustomAttributes.Any(a => a.AttributeType.Name == "SerializeField"),
                "Cloned dropdown arrows must retain remappable " + name);
        Check(Calls(Code("PanelScrollArrow", "OnEnable"), "UnityEvent", "AddListener") &&
            Calls(Code("PanelScrollArrow", "OnDisable"), "UnityEvent", "RemoveListener"), "Arrow listeners follow enabled lifetime");
        foreach (string method in new[] { "Dropdown", "Scroll" })
            Check(Calls(Code("PanelWidgets", method), "PanelWidgets", "AddScrollbar"), "Shared arrows cover " + method);
        Check(Calls(Code("PanelWidgets", "Dropdown"), "RectTransform", "set_offsetMin") &&
            Calls(Code("PanelWidgets", "Dropdown"), "RectTransform", "set_offsetMax"), "Short dropdown viewport uses stretch insets");
        Check(Calls(Code("PanelWidgets", "AddScrollbar"), "ScrollRect", "set_verticalScrollbar"), "Shared factory connects the native scrollbar");
        var updates = Code("SettingsPanel", "RefreshUpdateHeader");
        Check(updates.Any(i => i.LoadsConstant("Installing update · Keep the game open")) &&
            updates.Any(i => i.LoadsConstant("Update installed · Restart required")), "Exceptional update warnings remain global");
        Console.WriteLine("Verified rounded native graphics, no presentation polling, cloned arrow lifetimes and global update warnings (not live rendering).");
    }
}
