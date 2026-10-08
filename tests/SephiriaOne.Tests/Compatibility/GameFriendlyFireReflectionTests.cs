using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GameFriendlyFireReflectionTests
{
    internal static void Run(Assembly game)
    {
        Type Type(string name) => game.GetType(name, true)!;
        List<CodeInstruction> Code(string type, string method) => PatchProcessor.GetOriginalInstructions(AccessTools.DeclaredMethod(Type(type), method)).ToList();
        bool Call(CodeInstruction i, string owner, string name) => i.operand is MethodInfo m && m.DeclaringType?.Name == owner && m.Name == name;
        void Require(bool value, string why) { if (!value) throw new Exception("Friendly reflection contract: " + why); }
        int none = (int)Enum.Parse(Type("EDamageFromType"), "None");
        var apply = Code("UnitAvatar", "ApplyDamage");
        int thorns = apply.FindIndex(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, "Ability_Thorns"));
        Require(thorns > 0 && apply[thorns - 1].opcode == OpCodes.Ldarg_0, "Thorns origin is the damaged avatar");
        int create = apply.FindIndex(thorns, i => Call(i, "DamageInstance", "GetDamage"));
        Require(create > thorns && create - thorns < 24 && apply[create - 6].LoadsConstant(none) &&
            apply[create - 4].opcode == OpCodes.Initobj, "Thorns constructs a native None-type return");
        Require(apply[create + 1].IsStloc() && apply[create + 2].IsLdloc() && apply[create + 3].IsLdloc() &&
            Call(apply[create + 4], "CombatBehaviour", "ApplyDamage"), "Thorns immediately hits the incoming attacker");
        // Compare the attacker local to the original damage.origin extraction.
        int origin = apply.FindIndex(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.Name == "origin");
        int sourceStore = apply.FindIndex(origin, i => i.IsStloc());
        int sourceLocal = Local(apply[sourceStore]);
        // Native pattern matching first stores UnitAvatar unitAvatar2, then
        // copies it into the nullable attacker local used by the whole method.
        int sourceCopy = apply.FindIndex(sourceStore + 1, i => i.IsLdloc() && Local(i) == sourceLocal &&
            apply.IndexOf(i) + 1 < apply.Count && apply[apply.IndexOf(i) + 1].IsStloc());
        Require(sourceLocal >= 0 && sourceCopy > sourceStore && sourceCopy - sourceStore < 8 &&
            Local(apply[sourceCopy + 1]) == Local(apply[create + 2]), "Thorns return target is actual source, not follower owner");
        int invincible = apply.FindIndex(create, i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "isHitInvincibleEnabled");
        Require(invincible > create, "Thorns runs before new hit invulnerability, requiring recursion protection");

        var guard = Code("WeaponAddon_Reflect", "HandleGuard");
        int id = guard.FindIndex(i => Equals(i.operand, "Weapon_Reflect"));
        create = guard.FindIndex(i => Call(i, "DamageInstance", "GetDamage"));
        Require(id > 1 && create > id && guard[create - 4].LoadsConstant(none) &&
            Call(guard[create - 3], "Vector2", "get_zero") && Call(guard[create + 1], "CombatBehaviour", "ApplyDamage"),
            "weapon reflection constructs and directly applies native return damage");
        Require(guard.Any(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.DeclaringType == Type("DamageInstance") && f.Name == "damage") &&
            !guard.Any(i => i.operand is FieldInfo f && f.Name == "damageResult"), "weapon reflection uses native incoming raw damage");
        int attacker = guard.FindIndex(i => i.opcode == OpCodes.Isinst && Equals(i.operand, Type("UnitAvatar")));
        Require(attacker >= 0 && guard[attacker + 1].IsStloc() && Local(guard[attacker + 1]) == Local(guard[id - 2]),
            "weapon return target is incoming source");
        Require(Code("WeaponAddon_Reflect", "OnEnableAddon").Any(i => Call(i, "UnitAvatar", "add_OnGuardSucceeded")), "weapon return is bound to native guard");

        var parry = Code("Charm_VenomSporePouch", "OnParry");
        create = parry.FindIndex(i => Call(i, "DamageInstance", "GetDamage"));
        Require(create > 4 && parry[create - 4].LoadsConstant(none) && Call(parry[create - 3], "Vector2", "get_zero"), "Spore counter uses None-type damage");
        var ctor = PatchProcessor.GetOriginalInstructions(AccessTools.Constructor(Type("Charm_VenomSporePouch"))).ToList();
        id = ctor.FindIndex(i => Equals(i.operand, "Charm_VenomSporePouch"));
        Require(id >= 0 && ctor[id + 1].opcode == OpCodes.Stfld && ctor[id + 1].operand is FieldInfo f && f.Name == "damageId" &&
            parry.Any(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, f)), "Spore default native damage identifier");
        attacker = parry.FindIndex(i => i.opcode == OpCodes.Isinst && Equals(i.operand, Type("UnitAvatar")));
        int hit = parry.FindIndex(i => Call(i, "CombatBehaviour", "ApplyDamage"));
        Require(attacker >= 0 && parry[attacker + 1].IsStloc() && hit > create &&
            Local(parry[attacker + 1]) == Local(parry[hit - 2]), "Spore counter returns to incoming source");
        Require(Code("Charm_VenomSporePouch", "OnEnabledEffect").Any(i => Call(i, "UnitAvatar", "add_OnParry")), "Spore counter is bound to native parry");
        var set = Code("DamageInstance", "Set");
        foreach (string field in new[] { "origin", "id", "fromType", "damage" })
            Require(set.Any(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == field), "pooled damage resets " + field);
        Console.WriteLine("Verified native Thorns/weapon/parry return IDs, sources, None-type metadata, raw weapon input, pre-invulnerability timing and pooled resets (not live multiplayer).");
    }

    private static int Local(CodeInstruction i)
    {
        if (i.opcode == OpCodes.Ldloc_0 || i.opcode == OpCodes.Stloc_0) return 0;
        if (i.opcode == OpCodes.Ldloc_1 || i.opcode == OpCodes.Stloc_1) return 1;
        if (i.opcode == OpCodes.Ldloc_2 || i.opcode == OpCodes.Stloc_2) return 2;
        if (i.opcode == OpCodes.Ldloc_3 || i.opcode == OpCodes.Stloc_3) return 3;
        return i.operand is LocalVariableInfo local ? local.LocalIndex : -1;
    }
}
