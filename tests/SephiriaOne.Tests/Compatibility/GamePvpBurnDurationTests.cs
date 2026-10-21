using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GamePvpBurnDurationTests
{
    internal static void Run(Assembly game, Assembly addon)
    {
        Type Native(string name) => game.GetType(name, true)!;
        var runtime = addon.GetType("SephiriaOne.FriendlyFireRuntime", true)!;
        var origin = runtime.GetNestedType("DebuffOrigin", BindingFlags.NonPublic)!;
        List<CodeInstruction> Code(Type type, string name) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(type, name)).ToList();
        bool Call(CodeInstruction i, string type, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == type && m.Name == name;
        bool Calls(IEnumerable<CodeInstruction> code, string type, string name) => code.Any(i => Call(i, type, name));
        void Require(bool value, string why) { if (!value) throw new Exception("PvP Burn lifetime: " + why); }

        var debuff = Native("CharacterDebuff"); var burn = Native("CharacterDebuff_Burn"); var plasma = Native("CharacterDebuff_Plasma");
        Require(plasma.BaseType == debuff && burn.BaseType == debuff, "Plasma and Burn are distinct sibling debuffs");
        foreach (var family in new[] { burn, plasma })
        {
            var add = Code(family, "AddStack");
            Require(add.Any(i => i.LoadsConstant("BLUEBURNCHANGE")) && Calls(add, family.Name, "set_NetworkisBlueBurn"),
                family.Name + " implements Blue Flame as a native stack property");
            var tickDamage = Code(family, "CalculateTickDamage");
            Require(tickDamage.Any(i => i.LoadsConstant("BLUEBURNCHANGE")) && tickDamage.Any(i => i.LoadsConstant("BURNEVO")),
                family.Name + " keeps blue/evolved behavior on the same debuff type");
            var bonus = Code(family, "GetBonusPercent");
            Require(bonus.Any(i => i.LoadsConstant("BURNDURATION")) && Calls(bonus, "UnitAvatar", "GetCustomStat"),
                family.Name + " combines general and Burn-specific duration bonuses");
        }
        Require(Calls(Code(plasma, "RefreshDurationTimer"), "CharacterDebuff_Plasma", "Attack") &&
            Calls(Code(plasma, "RefreshDurationTimer"), "CharacterDebuff", "RefreshDurationTimer") &&
            Calls(Code(plasma, "DestroyInner"), "CharacterDebuff_Plasma", "Attack") &&
            Calls(Code(plasma, "Attack"), "CombatBehaviour", "ApplyDamage"),
            "Plasma retains native refresh/expiry attacks through shared damage and reaches the base refresh hook");
        var spawn = Code(debuff, "InitializeAndSpawn");
        Require(Calls(spawn, "CharacterDebuff", "GetAdditionalDuration") && Calls(spawn, "CharacterDebuff", "GetBonusPercent") &&
            spawn.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType?.Name == "Timer" && f.Name == "time") &&
            Calls(spawn, "NetworkServer", "Spawn"), "native initialization retains bonus calculation, timer configuration and stock replication");
        var update = Code(debuff, "Update");
        int tick = update.FindIndex(i => Call(i, "CharacterDebuff", "OnUpdate_Server"));
        int end = update.FindIndex(i => Call(i, "CharacterDebuff", "get_IsEndBuff"));
        int expiry = update.FindIndex(i => Call(i, "Timer", "Update"));
        Require(tick >= 0 && end > tick && expiry > end && Calls(update, "CharacterDebuff", "Destroy"),
            "native Update ticks before expiry and even during delayed despawn; prefix must guard both");
        var stack = Code(debuff, "AddStack");
        Require(Calls(stack, "CharacterDebuff", "RefreshDurationTimer") &&
            Calls(Code(debuff, "RefreshDurationTimer"), "Timer", "SetTimer") &&
            AccessTools.DeclaredMethod(burn, "RefreshDurationTimer") == null &&
            stack.FindIndex(i => Call(i, "CharacterDebuff", "RefreshDurationTimer")) <
            stack.FindIndex(i => Call(i, "CharacterDebuff", "get_MaxStackCount")),
            "Burn inherits the base refresh, including reapplication at maximum stacks");
        var destroy = Code(debuff, "Destroy");
        Require(destroy.FindIndex(i => Call(i, "CharacterDebuff", "set_IsEndBuff")) <
            destroy.FindIndex(i => Call(i, "CharacterDebuff", "RemoveStatus")) &&
            Calls(destroy, "CharacterDebuff", "DestroyInner") && Calls(destroy, "CharacterDebuff", "DestroyDelay") &&
            destroy.Any(i => i.LoadsConstant(0.5f)), "native Destroy marks ended, removes status and delays replicated destruction");
        var effectHooks = addon.GetType("SephiriaOne.FriendlyFireEffectHooks", true)!;
        var delay = (MethodInfo)AccessTools.Method(effectHooks, "Iterator").Invoke(null, new object[] { debuff, "DestroyDelay" })!;
        Require(PatchProcessor.GetOriginalInstructions(delay).Any(i => Call(i, "NetworkServer", "Destroy")), "native despawn reaches unmodified guests");

        var capture = Code(runtime, "BeforeDebuffSpawn");
        var appliedAt = AccessTools.Field(origin, "BurnAppliedAt");
        Require(Calls(capture, "Time", "get_time") && capture.Any(i => i.StoresField(appliedAt)),
            "application time belongs to the same current-connection receipt as the debuff");
        var refresh = Code(runtime, "AfterBurnRefresh");
        var familyCheck = Code(runtime, "IsBurnFamily");
        Require(familyCheck.Any(i => i.opcode == OpCodes.Isinst && Equals(i.operand, burn)) &&
            familyCheck.Any(i => i.opcode == OpCodes.Isinst && Equals(i.operand, plasma)),
            "shared family classification includes Burn, Plasma and their subtypes without matching unrelated elements");
        Require(Calls(refresh, "FriendlyFireRuntime", "IsBurnFamily") &&
            Calls(refresh, "DebuffOrigin", "get_Valid") && Calls(refresh, "CharacterDebuff", "get_IsEndBuff") &&
            refresh.Any(i => i.LoadsField(AccessTools.Field(origin, "BurnEnded"))) &&
            Calls(refresh, "Time", "get_time") && refresh.Any(i => i.StoresField(appliedAt)),
            "only valid, live Burn-family receipts track native refreshes");
        var install = Code(effectHooks, "Install");
        Require(install.Any(i => i.LoadsConstant("RefreshDurationTimer")) && install.Any(i => i.LoadsConstant("AfterBurnRefresh")),
            "shared effect installation registers the Burn refresh hook");
        var before = Code(runtime, "BeforeDebuffUpdate");
        Require(before.FindIndex(i => Call(i, "DebuffOrigin", "get_Valid")) <
            before.FindIndex(i => Call(i, "FriendlyFireRuntime", "UpdateWithinBurnLifetime")), "connection validation precedes the duration cap");
        var cap = Code(runtime, "UpdateWithinBurnLifetime");
        Require(Calls(cap, "FriendlyFireRuntime", "IsBurnFamily") && Calls(cap, "FriendlyFireSettings", "get_DotMaxDuration") &&
            Calls(cap, "FriendlyFireRuntime", "CanAffectTeam") && Calls(cap, "CharacterDebuff", "Destroy") &&
            cap.Any(i => i.StoresField(AccessTools.Field(origin, "BurnEnded"))) &&
            !cap.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType?.Name == "Timer"),
            "Burn-family cap uses shared policy, native destruction and no timer/stat mutation");
        Require(cap.FindIndex(i => i.StoresField(AccessTools.Field(origin, "BurnEnded"))) <
            cap.FindIndex(i => Call(i, "CharacterDebuff", "Destroy")), "retire before native callbacks to prevent duplicate expiry");
        Require(Calls(Code(runtime, "Clear"), "FriendlyFireRuntime", "ClearDebuffOrigins"), "unload clears the shared lifetime storage");
        var panel = addon.GetType("SephiriaOne.SettingsPanel", true)!;
        var build = Code(panel, "BuildPvpDotEditor");
        Require(build.Any(i => i.LoadsConstant("Burn-family max duration (s)")) &&
            build.Any(i => i.LoadsConstant("Burn / Plasma Burn / Blue Flame")) && build.Any(i => i.LoadsConstant(812f)),
            "Duration row labels exact coverage and extends existing scroll content without shrinking the viewport");
        var applyDuration = Code(panel, "ApplyPvpDotDuration"); var resetDuration = Code(panel, "ResetPvpDotDuration");
        Require(applyDuration.Any(i => i.LoadsConstant("/one friendlyfire dot-duration ")) &&
            Calls(applyDuration, "SettingsPanel", "Execute") && Calls(applyDuration, "SettingsPanel", "ClearPvpDotDuration") &&
            resetDuration.Any(i => i.LoadsConstant("/one friendlyfire dot-duration reset")) &&
            Calls(resetDuration, "SettingsPanel", "Execute") && Calls(resetDuration, "SettingsPanel", "ClearPvpDotDuration"),
            "Duration Apply/reset use shared host commands and clear their own draft");
        var panelRefresh = Code(panel, "RefreshPvpDots");
        Require(Calls(panelRefresh, "FriendlyFireSettings", "get_DotMaxDuration") &&
            Calls(panelRefresh, "FriendlyFireSettings", "get_DotMaxDurationNumber") &&
            Calls(panelRefresh, "SettingsSnapshot", "get_CanMutate") && Calls(panelRefresh, "SettingsSnapshot", "get_FriendlyFireAvailable") &&
            panelRefresh.Any(i => i.LoadsField(AccessTools.Field(panel, "pvpDotDurationDraft"))) &&
            panelRefresh.Any(i => i.LoadsField(AccessTools.Field(panel, "pvpDotDurationLast"))),
            "Refresh preserves typed drafts and avoids repeated formatting of unchanged duration");
        Require(Calls(Code(panel, "ClearPvpDots"), "SettingsPanel", "ClearPvpDotDuration") &&
            Code(panel, "BuildPage").Any(i => i.StoresField(AccessTools.Field(panel, "pvpDotDuration"))) &&
            !Code(panel, "ClearPvpDotDuration").Any(i => i.LoadsField(AccessTools.Field(panel, "pvpDotEditors"))),
            "Duration draft follows page/session lifetime and does not clear sibling interval drafts");
        Console.WriteLine("Verified Burn/Plasma/Blue Flame duration and refresh contracts, native Plasma expiry attack, live shared duration cap and guest despawn (not live multiplayer).");
    }
}
