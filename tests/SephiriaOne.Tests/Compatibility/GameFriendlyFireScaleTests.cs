using System.Reflection;
using HarmonyLib;

internal static class GameFriendlyFireScaleTests
{
    internal static void Run(Assembly addon)
    {
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        List<CodeInstruction> Code(string name) => PatchProcessor.GetOriginalInstructions(AccessTools.Method(panel, name)).ToList();
        bool Call(CodeInstruction instruction, string type, string name) => instruction.operand is MethodInfo method &&
            method.DeclaringType?.Name == type && method.Name == name;
        void Require(bool condition, string why) { if (!condition) throw new Exception("Fractional combat UI: " + why); }
        var build = Code("BuildCombatEditor");
        int wholeNumbers = build.FindIndex(i => Call(i, "Slider", "set_wholeNumbers"));
        Require(wholeNumbers > 0 && build[wholeNumbers - 1].LoadsConstant(0), "slider permits fractional values");
        Require(build.Any(i => Call(i, "PanelWidgets", "Input")), "exact percentage entry exists beside slider");
        var edit = Code("EditFriendlyDamage"); var drag = Code("DragFriendlyDamage"); var apply = Code("ApplyFriendlyDamage");
        Require(edit.Any(i => Call(i, "FriendlyFireCommand", "TryPercent")), "typed input shares command validation");
        Require(!edit.Concat(drag).Any(i => Call(i, "SettingsPanel", "Execute")), "typing/dragging never commits policy");
        Require(edit.Any(i => Call(i, "Slider", "SetValueWithoutNotify")) && drag.Any(i => Call(i, "TMP_InputField", "SetTextWithoutNotify")),
            "paired controls update without recursive change events");
        Require(drag.Any(i => Call(i, "Math", "Round")) && drag.Any(i => i.LoadsConstant(2)), "slider drafts round to supported precision");
        Require(apply.Any(i => Call(i, "TMP_InputField", "get_text")) && apply.Any(i => Call(i, "SettingsPanel", "Execute")) &&
            !apply.Any(i => Call(i, "Slider", "get_value")), "Apply uses exact input through shared command without integer slider cast");
        var refresh = Code("RefreshCombat");
        Require(refresh.Any(i => Call(i, "FriendlyFireSettings", "get_Number")) &&
            refresh.Any(i => i.operand is FieldInfo field && field.Name == "friendlyDraft"), "refresh preserves edits and shows canonical saved precision");
        Require(Code("ClearInput").Any(i => i.operand is FieldInfo field && field.Name == "friendlyDraft"), "existing scope reset clears combat draft");
        Console.WriteLine("Verified fractional combat slider/input, shared parser/Apply and no-notify draft wiring (not live Unity input).");
    }
}
