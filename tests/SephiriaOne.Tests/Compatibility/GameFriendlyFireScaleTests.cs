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
        var build = Code("BuildFriendlyDamageEditor");
        var balance = Code("BuildPvpBalanceEditor");
        Require(Code("BuildPage").Any(i => Call(i, "SettingsPanel", "BuildPvpBalanceEditor")) &&
            !Code("BuildCombatEditor").Any(i => Call(i, "SettingsPanel", "BuildPvpBalanceEditor")) &&
            balance.Any(i => i.LoadsConstant("PvP balancing")) && balance.Any(i => i.LoadsConstant("/one friendlyfire balance")),
            "Dedicated page exposes balancing through the same shared command without rebuilding it inside Combat");
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var scale = PatchProcessor.GetOriginalInstructions(AccessTools.Method(runtime, "Scale")).ToList();
        Require(scale.Any(i => Call(i, "FriendlyFireSettings", "get_Balance")) && scale.Any(i => Call(i, "UnitAvatar", "get_MaxHp")) &&
            scale.Count(i => Call(i, "FriendlyFireBalance", "Soften")) == 1, "Player softening reads current balancing and native maximum HP once per resolved hit");
        Require(scale.Count(i => Call(i, "FriendlyFireRuntime", "LimitBalancedDamage")) == 1,
            "Rapid-hit budget shares the verified pre-shield balance boundary");
        Require(scale.Any(i => Call(i, "FriendlyFireSettings", "get_RefillScale")), "Damage pipeline reads the configured refill");
        var revive = PatchProcessor.GetOriginalInstructions(AccessTools.Method(runtime, "BeforeRevive")).ToList();
        Require(revive.Any(i => Call(i, "FriendlyFireRuntime", "ResetBalanceLife")), "Native revival retires the player's budget life");
        var finish = PatchProcessor.GetOriginalInstructions(AccessTools.Method(runtime, "AfterHit")).ToList();
        Require(finish.Any(i => Call(i, "FriendlyFireRuntime", "FinishBalancedDamage")), "Damage finalizer reconciles reserved allowance after native accounting");
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
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyDamage")), "existing scope reset clears combat draft");
        var refill = Code("ApplyFriendlyRefill");
        Require(apply.Any(i => Call(i, "SettingsPanel", "ClearFriendlyDamage")) && refill.Any(i => Call(i, "SettingsPanel", "ClearFriendlyRefill")),
            "Each successful Apply clears only its own draft, preserving the other pending field");
        Require(balance.Count(i => Call(i, "PanelWidgets", "Input")) == 4 && !balance.Any(i => i.LoadsConstant("PvP refill (%/s)")) &&
            Code("BuildPvpRapidEditor").Any(i => i.LoadsConstant("PvP refill (%/s)")),
            "Balance holds guard, Perfect Guard and paired amplification inputs; refill is exclusively on Rapid Hits");
        Require(refill.Any(i => i.LoadsConstant("/one friendlyfire refill ")) && refill.Any(i => Call(i, "TMP_InputField", "get_text")) &&
            refill.Any(i => Call(i, "SettingsPanel", "Execute")), "Refill Apply uses exact text through the shared command");
        Require(!Code("EditFriendlyRefill").Any(i => Call(i, "SettingsPanel", "Execute")) &&
            Code("RefreshPvpRapid").Any(i => i.operand is FieldInfo field && field.Name == "friendlyRefillDraft") &&
            Code("RefreshPvpRapid").Any(i => Call(i, "FriendlyFireSettings", "get_RefillNumber")), "Refill typing stays local and Rapid Hits refresh preserves the draft");
        Require(Code("ClearInput").Any(i => Call(i, "SettingsPanel", "ClearFriendlyRefill")), "Scope resets clear refill drafts too");
        Require(!Code("ClearFriendlyDamage").Any(i => i.operand is FieldInfo field && field.Name == "friendlyRefillDraft") &&
            !Code("ClearFriendlyRefill").Any(i => i.operand is FieldInfo field && field.Name == "friendlyDraft"), "Per-field resets do not touch the sibling draft");
        Require(Code("Execute").Any(i => Call(i, "Action", "Invoke")), "Successful execution invokes its field-specific cleanup");
        Console.WriteLine("Verified fractional combat slider/input, shared parser/Apply and no-notify draft wiring (not live Unity input).");
    }
}
