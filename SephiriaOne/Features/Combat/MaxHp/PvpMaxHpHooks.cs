using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static class PvpMaxHpHooks
    {
        private const string Id = "SephiriaOne.PvpMaxHp";
        private static Harmony harmony;
        private static Func<WeaponAddonGreatsword_BoneBlood, UnitAvatar> boneAvatar;
        private static Func<StatusInstance, UnitAvatar> statusTarget;
        private static readonly FieldInfo hp = AccessTools.Field(typeof(UnitAvatar), nameof(UnitAvatar.hp));
        private static readonly MethodInfo maximum = AccessTools.PropertyGetter(typeof(UnitAvatar), nameof(UnitAvatar.MaxHp));

        internal static void Install()
        {
            if (harmony != null) return;
            var add = RequireMethod(typeof(UnitAvatar), nameof(UnitAvatar.AddMaxHp), typeof(void), typeof(float), typeof(bool));
            var percent = RequireMethod(typeof(UnitAvatar), nameof(UnitAvatar.AddMaxHpPercent), typeof(void), typeof(float), typeof(bool));
            var apply = RequireMethod(typeof(StatusInstance_HPCurse), "ApplyStatusInner", typeof(void), typeof(bool));
            var remove = RequireMethod(typeof(StatusInstance_HPCurse), "RemoveStatusInner", typeof(void));
            var engage = RequireMethod(typeof(WeaponAddonGreatsword_BoneBlood), "Engage", typeof(void));
            var disengage = RequireMethod(typeof(WeaponAddonGreatsword_BoneBlood), "Disengage", typeof(void), typeof(bool), typeof(bool));
            var update = RequireMethod(typeof(WeaponAddonGreatsword_BoneBlood), "Update", typeof(void));
            var avatar = RequireMethod(typeof(WeaponAddonGreatsword_BoneBlood), "get_Avatar", typeof(UnitAvatar));
            var target = RequireMethod(typeof(StatusInstance), "get_CurrentTarget", typeof(UnitAvatar));
            var save = RequireMethod(typeof(PlayerSpawner), nameof(PlayerSpawner.SaveCurrentSessionData), typeof(void));
            var initialize = NativePlayerInitialization.ResolveInitialize(typeof(PlayerSpawner));
            if (!ValidateNativeFields() || !ValidateAdd(Code(add), false) || !ValidateAdd(Code(percent), true) ||
                !ValidateCurse(Code(apply), true) || !ValidateCurse(Code(remove), false) ||
                !ValidateBone(Code(engage), "Engage") || !ValidateBone(Code(disengage), "Disengage") ||
                !ValidateBone(Code(update), "Update") || !ValidateSave(Code(save)))
                throw new InvalidOperationException("Native maximum HP mutation, save or replication contract changed.");
            // Use the framework delegate API; co-installed HarmonyX lacks MethodDelegate.
            boneAvatar = (Func<WeaponAddonGreatsword_BoneBlood, UnitAvatar>)Delegate.CreateDelegate(
                typeof(Func<WeaponAddonGreatsword_BoneBlood, UnitAvatar>), avatar);
            statusTarget = (Func<StatusInstance, UnitAvatar>)Delegate.CreateDelegate(typeof(Func<StatusInstance, UnitAvatar>), target);
            var instance = new Harmony(Id);
            try
            {
                PatchScope(instance, add, nameof(BeforeUnit));
                PatchScope(instance, percent, nameof(BeforeUnit));
                PatchScope(instance, apply, nameof(BeforeCurse));
                PatchScope(instance, remove, nameof(BeforeCurse));
                PatchScope(instance, engage, nameof(BeforeEngage));
                PatchScope(instance, disengage, nameof(BeforeDisengage));
                PatchScope(instance, update, nameof(BeforeBoneUpdate));
                instance.Patch(initialize,
                    prefix: Ordered(nameof(BeforeInitialize), int.MaxValue),
                    finalizer: Ordered(nameof(AfterInitialize), int.MinValue));
                instance.Patch(save, transpiler: new HarmonyMethod(typeof(PvpMaxHpHooks), nameof(RewriteSave)));
                harmony = instance;
            }
            catch
            {
                PvpMaxHpRuntime.Clear();
                instance.UnpatchAll(Id);
                boneAvatar = null;
                statusTarget = null;
                throw;
            }
        }

        internal static void Uninstall()
        {
            if (harmony == null) return;
            harmony.UnpatchAll(Id);
            harmony = null;
            boneAvatar = null;
            statusTarget = null;
        }

        private static HarmonyMethod Ordered(string method, int priority) =>
            new HarmonyMethod(typeof(PvpMaxHpHooks), method) { priority = priority };
        private static void PatchScope(Harmony instance, MethodInfo method, string prefix) => instance.Patch(method,
            prefix: Ordered(prefix, int.MaxValue), finalizer: Ordered(nameof(AfterNative), int.MinValue));
        private static MethodInfo RequireMethod(Type owner, string name, Type result, params Type[] parameters)
        {
            var method = AccessTools.DeclaredMethod(owner, name, parameters);
            if (method == null || method.IsStatic || method.ReturnType != result || method.ContainsGenericParameters)
                throw new MissingMethodException(owner.Name + "." + name + " changed.");
            return method;
        }
        private static List<CodeInstruction> Code(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        private static bool Reads(CodeInstruction instruction, Type owner, string field) =>
            instruction.opcode == OpCodes.Ldfld && instruction.operand is FieldInfo f && f.DeclaringType == owner && f.Name == field;
        private static bool Writes(CodeInstruction instruction, Type owner, string field) =>
            instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo f && f.DeclaringType == owner && f.Name == field;
        private static MethodInfo Setter(string field) => AccessTools.PropertySetter(typeof(UnitAvatar), "Network" + field);

        internal static bool ValidateAdd(IEnumerable<CodeInstruction> instructions, bool percent)
        {
            var code = instructions.ToList();
            string field = percent ? "finalMaxHp" : "maxHp";
            var setter = Setter(field);
            int at = code.FindIndex(i => i.Calls(setter));
            int ratio = code.FindIndex(i => i.Calls(maximum));
            return setter != null && maximum != null && code.Count(i => i.Calls(setter)) == 1 && at >= 5 &&
                code[at - 5].opcode == OpCodes.Ldarg_0 && code[at - 4].opcode == OpCodes.Ldarg_0 &&
                Reads(code[at - 3], typeof(UnitAvatar), field) && code[at - 2].opcode == OpCodes.Ldarg_1 && code[at - 1].opcode == OpCodes.Add &&
                ratio >= 3 && ratio < at && code[ratio - 3].opcode == OpCodes.Ldarg_0 &&
                Reads(code[ratio - 2], typeof(UnitAvatar), "hp") && code[ratio - 1].opcode == OpCodes.Ldarg_0 &&
                ratio + 1 < code.Count && code[ratio + 1].opcode == OpCodes.Div &&
                code.Count(i => i.Calls(Setter("hp"))) == 2 && code.Count(i => i.Calls(maximum)) == 4 &&
                code.Any(i => Reads(i, typeof(UnitAvatar), "isHPCursed"));
        }

        internal static bool ValidateCurse(IEnumerable<CodeInstruction> instructions, bool apply)
        {
            var code = instructions.ToList();
            var cursed = Setter("isHPCursed");
            var value = Setter("cursedMaxHp");
            int at = code.FindIndex(i => i.Calls(cursed));
            int valueAt = code.FindIndex(i => i.Calls(value));
            return cursed != null && value != null && at >= 3 && valueAt > at &&
                code.Count(i => i.Calls(cursed)) == 1 && code.Count(i => i.Calls(value)) == 1 &&
                code[at - 1].opcode == OpCodes.Conv_I1 && code[at - 2].opcode == (apply ? OpCodes.Add : OpCodes.Sub) &&
                code[at - 3].LoadsConstant(1) && code.Any(i => Reads(i, typeof(UnitAvatar), "isHPCursed")) &&
                code.Count(i => i.Calls(maximum)) == 2 && code.Count(i => i.Calls(AccessTools.DeclaredMethod(typeof(UnitAvatar), "SetHp", new[] { typeof(float) }))) == 1 &&
                (apply ? code.Any(i => i.operand is MethodInfo m && m.DeclaringType == typeof(StatusInstance) && m.Name == "get_Value") : code[valueAt - 1].LoadsConstant(0));
        }

        internal static bool ValidateBone(IEnumerable<CodeInstruction> instructions, string operation)
        {
            var code = instructions.ToList();
            var owner = typeof(WeaponAddonGreatsword_BoneBlood);
            var curse = Setter("isHPCursed");
            var value = Setter("cursedMaxHp");
            int curseAt = code.FindIndex(i => i.Calls(curse));
            int valueAt = code.FindIndex(i => i.Calls(value));
            if (curse == null || value == null || curseAt < 1 || valueAt <= curseAt ||
                code.Count(i => i.Calls(curse)) != 1 || code.Count(i => i.Calls(value)) != 1) return false;
            if (operation == "Engage")
                return code[curseAt - 1].LoadsConstant(1) && Reads(code[valueAt - 1], owner, "engagedMaxHp") &&
                    code.Take(curseAt).Any(i => Writes(i, owner, "cachedHpCursed")) &&
                    code.Take(curseAt).Any(i => Writes(i, owner, "cachedCursedMaxHp")) &&
                    code.Take(curseAt).Any(i => i.Calls(AccessTools.DeclaredMethod(owner, "GetUncappedMaxHp", new[] { typeof(UnitAvatar) })));
            if (operation == "Disengage")
                return Reads(code[curseAt - 1], owner, "cachedHpCursed") && Reads(code[valueAt - 1], owner, "cachedCursedMaxHp") &&
                    code.Take(curseAt).Any(i => Reads(i, owner, "engagedAvatar"));
            if (operation != "Update") return false;
            return code[curseAt - 1].LoadsConstant(1) && Reads(code[valueAt - 1], owner, "engagedMaxHp") &&
                code.Take(curseAt).Any(i => Reads(i, owner, "isEngaged")) && code.Take(curseAt).Any(i => Reads(i, owner, "engagedAvatar")) &&
                code.Take(curseAt).Any(i => Reads(i, typeof(UnitAvatar), "isHPCursed")) &&
                code.Take(curseAt).Any(i => Reads(i, typeof(UnitAvatar), "cursedMaxHp")) &&
                code.Any(i => i.operand is MethodInfo m && m.Name == "get_isServer");
        }

        internal static bool ValidateSave(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var avatar = AccessTools.PropertyGetter(typeof(PlayerSpawner), nameof(PlayerSpawner.PlayerAvatar));
            int count = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Ldfld || !Equals(code[i].operand, hp)) continue;
                if (i < 1 || i + 1 >= code.Count || !code[i - 1].Calls(avatar) ||
                    !(code[i + 1].operand is MethodInfo save) || !code[i + 1].Calls(save) || save.IsStatic ||
                    save.Name != "SetFloat" || save.ReturnType != typeof(void) ||
                    !save.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(float) })) return false;
                count++;
            }
            return hp?.FieldType == typeof(float) && count == 2;
        }

        internal static IEnumerable<CodeInstruction> RewriteSave(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            if (!ValidateSave(code)) throw new InvalidOperationException("Native saved HP reads changed during patching.");
            var replacement = AccessTools.DeclaredMethod(typeof(PvpMaxHpRuntime), nameof(PvpMaxHpRuntime.NativeHpForSave), new[] { typeof(UnitAvatar) });
            foreach (var instruction in code)
            {
                if (instruction.opcode != OpCodes.Ldfld || !Equals(instruction.operand, hp)) continue;
                // Identical stack shape; retained labels/blocks preserve native control flow.
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            return code;
        }

        internal static bool ValidateNativeFields()
        {
            var unit = typeof(UnitAvatar);
            var fields = new[] { "hp", "maxHp", "finalMaxHp", "isHPCursed", "cursedMaxHp" };
            var types = new[] { typeof(float), typeof(float), typeof(float), typeof(sbyte), typeof(int) };
            var bits = new long[] { 16, 512, 1024, 2048, 4096 };
            var serialize = AccessTools.DeclaredMethod(unit, "SerializeSyncVars");
            var deserialize = AccessTools.DeclaredMethod(unit, "DeserializeSyncVars");
            if (maximum == null || serialize == null || deserialize == null) return false;
            var send = Code(serialize); var receive = Code(deserialize);
            for (int n = 0; n < fields.Length; n++)
            {
                var field = AccessTools.Field(unit, fields[n]);
                var setter = Setter(fields[n]);
                if (field == null || field.IsStatic || field.FieldType != types[n] || setter == null) return false;
                var code = Code(setter);
                if (!code.Any(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, field)) || !code.Any(i => i.LoadsConstant(bits[n])) ||
                    !code.Any(i => i.operand is MethodInfo m && m.Name == "GeneratedSyncVarSetter") ||
                    send.Count(i => i.opcode == OpCodes.Ldfld && Equals(i.operand, field)) != 2 ||
                    receive.Count(i => i.opcode == OpCodes.Ldflda && Equals(i.operand, field)) != 2) return false;
            }
            var getter = Code(maximum);
            if (getter.Count(i => Reads(i, unit, "maxHp")) != 2 || getter.Count(i => Reads(i, unit, "finalMaxHp")) != 1 ||
                getter.Count(i => Reads(i, unit, "cursedMaxHp")) != 1 || getter.Count(i => Reads(i, unit, "isHPCursed")) != 1 ||
                !getter.Any(i => i.opcode == OpCodes.Mul) || !getter.Any(i => i.opcode == OpCodes.Div) ||
                !getter.Any(i => i.opcode == OpCodes.Add) || !getter.Any(i => i.LoadsConstant(100f))) return false;
            foreach (string name in new[] { "UI_HPBar", "UI_MultiplayerHPBar" })
            {
                var owner = unit.Assembly.GetType(name);
                var update = owner == null ? null : AccessTools.DeclaredMethod(owner, "Update", Type.EmptyTypes);
                if (update == null) return false;
                var code = Code(update);
                if (!code.Any(i => i.Calls(maximum)) || !code.Any(i => Reads(i, unit, "hp"))) return false;
            }
            return true;
        }

        private static void Begin(UnitAvatar avatar, out UnitAvatar state)
        {
            state = null;
            if (!NetworkServer.active || !(avatar is PlayerAvatar)) return;
            state = avatar;
            PvpMaxHpRuntime.BeginNative(avatar);
        }
        private static void BeforeUnit(UnitAvatar __instance, out UnitAvatar __state) => Begin(__instance, out __state);
        private static void BeforeCurse(StatusInstance_HPCurse __instance, out UnitAvatar __state) => Begin(statusTarget(__instance), out __state);
        private static void BeforeEngage(WeaponAddonGreatsword_BoneBlood __instance, out UnitAvatar __state) => Begin(boneAvatar(__instance), out __state);
        private static void BeforeDisengage(UnitAvatar ___engagedAvatar, out UnitAvatar __state) => Begin(___engagedAvatar, out __state);
        private static bool BeforeBoneUpdate(WeaponAddonGreatsword_BoneBlood __instance, bool ___isEngaged,
            UnitAvatar ___engagedAvatar, int ___engagedMaxHp, out UnitAvatar __state)
        {
            __state = null;
            if (!NetworkServer.active || !___isEngaged || !__instance.isServer || !___engagedAvatar) return true;
            // A projected curse differs on purpose. No reflection, unprojection,
            // allocations or native writes are needed in this stable-frame path.
            if (PvpMaxHpRuntime.HasNativeCurse(___engagedAvatar, ___engagedMaxHp)) return false;
            if (___engagedAvatar.isHPCursed > 0 && ___engagedAvatar.cursedMaxHp == ___engagedMaxHp) return true;
            Begin(___engagedAvatar, out __state);
            return true;
        }
        private static void BeforeInitialize(PlayerSpawner __instance, out UnitAvatar __state) => Begin(__instance.PlayerAvatar, out __state);
        private static Exception AfterNative(UnitAvatar __state, Exception __exception)
        {
            if (!ReferenceEquals(__state, null)) PvpMaxHpRuntime.EndNative(__state, __exception == null);
            return __exception;
        }
        private static Exception AfterInitialize(UnitAvatar __state, bool __result, Exception __exception)
        {
            if (!ReferenceEquals(__state, null)) PvpMaxHpRuntime.EndNative(__state, __exception == null && __result);
            return __exception;
        }
    }
}
