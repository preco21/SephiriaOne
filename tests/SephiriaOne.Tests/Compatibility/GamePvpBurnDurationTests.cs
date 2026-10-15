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

        var debuff = Native("CharacterDebuff"); var burn = Native("CharacterDebuff_Burn");
        Require(Native("CharacterDebuff_Plasma").BaseType == debuff && burn.BaseType == debuff,
            "Plasma is a separate debuff; blue Burn uses the same Burn type");
        Require(Calls(Code(burn, "AddStack"), "CharacterDebuff_Burn", "set_NetworkisBlueBurn"), "blue appearance remains a native Burn stack property");
        var bonus = Code(burn, "GetBonusPercent");
        Require(bonus.Any(i => i.LoadsConstant("BURNDURATION")) && Calls(bonus, "UnitAvatar", "GetCustomStat"),
            "native Burn combines general and Burn-specific duration bonuses");
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
        Require(refresh.Any(i => i.opcode == OpCodes.Isinst && Equals(i.operand, burn)) &&
            Calls(refresh, "DebuffOrigin", "get_Valid") && Calls(refresh, "CharacterDebuff", "get_IsEndBuff") &&
            refresh.Any(i => i.LoadsField(AccessTools.Field(origin, "BurnEnded"))) &&
            Calls(refresh, "Time", "get_time") && refresh.Any(i => i.StoresField(appliedAt)),
            "only valid, live Burn receipts track native refreshes");
        var install = Code(effectHooks, "Install");
        Require(install.Any(i => i.LoadsConstant("RefreshDurationTimer")) && install.Any(i => i.LoadsConstant("AfterBurnRefresh")),
            "shared effect installation registers the Burn refresh hook");
        var before = Code(runtime, "BeforeDebuffUpdate");
        Require(before.FindIndex(i => Call(i, "DebuffOrigin", "get_Valid")) <
            before.FindIndex(i => Call(i, "FriendlyFireRuntime", "UpdateWithinBurnLifetime")), "connection validation precedes the duration cap");
        var cap = Code(runtime, "UpdateWithinBurnLifetime");
        Require(cap.Any(i => i.opcode == OpCodes.Isinst && Equals(i.operand, burn)) && cap.Any(i => i.LoadsConstant(2f)) &&
            Calls(cap, "FriendlyFireRuntime", "CanAffectTeam") && Calls(cap, "CharacterDebuff", "Destroy") &&
            cap.Any(i => i.StoresField(AccessTools.Field(origin, "BurnEnded"))) &&
            !cap.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.DeclaringType?.Name == "Timer"),
            "Burn-only cap uses shared policy, native destruction and no timer/stat mutation");
        Require(cap.FindIndex(i => i.StoresField(AccessTools.Field(origin, "BurnEnded"))) <
            cap.FindIndex(i => Call(i, "CharacterDebuff", "Destroy")), "retire before native callbacks to prevent duplicate expiry");
        Require(Calls(Code(runtime, "Clear"), "FriendlyFireRuntime", "ClearDebuffOrigins"), "unload clears the shared lifetime storage");
        Console.WriteLine("Verified Burn duration/refresh/late-tick contracts, shared origin validation, two-second cap and native guest despawn (not live multiplayer).");
    }
}
