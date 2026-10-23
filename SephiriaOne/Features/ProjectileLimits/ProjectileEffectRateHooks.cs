using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // RPC-created attacks have client-local lifetimes. Rate admission happens at
    // their creation sender; receivers and existing objects always remain native.
    internal static class ProjectileEffectRateHooks
    {
        // These are immediate call handoffs, not ambient scopes. Each native
        // entry consumes its request into a local before invoking any callbacks.
        [ThreadStatic] private static object requestedBatch;
        [ThreadStatic] private static object requestedSword;

        private static MethodInfo SwordSender => Require(typeof(Charm_IceSword), "RpcCreateSwords");
        private static MethodInfo GuillotineSender => Require(typeof(Charm_Guillotine), "RpcCreateGuillotine");
        private static MethodInfo Callback => Require(typeof(ComboEffect_FlameSword), "HandleAttackUnit");
        private static MethodInfo Batch => Require(typeof(ComboEffect_FlameSword), "ServerFireSword");
        private static MethodInfo Fire => Require(typeof(ComboEffect_FlameSword), "FireSword");
        private static MethodInfo Use => Require(typeof(ComboEffect_FlameSword), "UseSword");
        private static MethodInfo Create => Require(typeof(ComboEffect_FlameSword), "CreateFlameSword");
        private static MethodInfo AutoSearch => Require(typeof(ComboEffect_FlameSword), "AutoSearchNearEnemyAndFireSword");
        private static MethodInfo Local(string name) => Require(typeof(ProjectileEffectRateHooks), name);
        private static HarmonyMethod Hook(string name) => new HarmonyMethod(Local(name));
        private static MethodInfo Require(Type type, string name) => AccessTools.DeclaredMethod(type, name)
            ?? throw new MissingMethodException(type.Name, name);

        internal static void Install(Harmony harmony)
        {
            ValidateSender(SwordSender, PatchProcessor.GetOriginalInstructions(SwordSender));
            ValidateSender(GuillotineSender, PatchProcessor.GetOriginalInstructions(GuillotineSender));
            ValidateCallback(PatchProcessor.GetOriginalInstructions(Callback));
            ValidateBatch(PatchProcessor.GetOriginalInstructions(Batch));
            ValidateFireSword(PatchProcessor.GetOriginalInstructions(Fire));
            harmony.Patch(SwordSender, prefix: Hook(nameof(BeforeIceSword)));
            harmony.Patch(GuillotineSender, prefix: Hook(nameof(BeforeGuillotine)));
            harmony.Patch(Callback, transpiler: Hook(nameof(RewriteCallback)));
            harmony.Patch(Batch, transpiler: Hook(nameof(RewriteBatch)));
            harmony.Patch(Fire, transpiler: Hook(nameof(RewriteFireSword)));
        }

        private static bool BeforeIceSword(Charm_IceSword __instance) =>
            ProjectileLimitRuntime.AdmitEffect(__instance.NetworkAvatar, "ice-sword");
        private static bool BeforeGuillotine(Charm_Guillotine __instance) =>
            ProjectileLimitRuntime.AdmitEffect(__instance.NetworkAvatar, "ice-guillotine");
        private static bool AdmitFlameSword(ComboEffect_FlameSword instance) =>
            ProjectileLimitRuntime.AdmitEffect(instance.Networkavatar, "frost-flame-sword");

        private static void FrostBatch(ComboEffect_FlameSword instance, Vector3 motionTo,
            bool isDirectAttack, bool isMagic, int bonus, bool autoSearchNearEnemy)
        {
            object previous = requestedBatch;
            requestedBatch = instance;
            try { instance.ServerFireSword(motionTo, isDirectAttack, isMagic, bonus, autoSearchNearEnemy); }
            finally { requestedBatch = previous; }
        }
        private static bool BatchSword(ComboEffect_FlameSword instance, Vector3 motionFrom, Vector3 motionTo, bool frost)
        {
            object previous = requestedSword;
            requestedSword = frost ? instance : null;
            try { return instance.FireSword(motionFrom, motionTo); }
            finally { requestedSword = previous; }
        }
        internal static bool TakeBatchRequest(object instance) => Take(ref requestedBatch, instance);
        internal static bool TakeSwordRequest(object instance) => Take(ref requestedSword, instance);
        private static bool Take(ref object request, object instance)
        {
            bool matches = instance != null && ReferenceEquals(request, instance);
            request = null;
            return matches;
        }

        private static void Signature(MethodInfo method, Type result, params Type[] parameters)
        {
            if (method.IsStatic || method.ReturnType != result ||
                !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters))
                throw new InvalidOperationException("Native effect signature changed: " + method);
        }
        private static void PlainBody(List<CodeInstruction> code, string name)
        {
            if (code.Count == 0 || code.Any(i => i.blocks.Count != 0))
                throw new InvalidOperationException("Native effect exception scope changed: " + name);
        }
        private static int SingleCall(List<CodeInstruction> code, MethodInfo method)
        {
            int[] calls = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(method)).ToArray();
            if (calls.Length != 1) throw new InvalidOperationException("Native effect call count changed: " + method);
            return calls[0];
        }

        internal static void ValidateSender(MethodInfo sender, IEnumerable<CodeInstruction> instructions)
        {
            bool sword = sender == SwordSender;
            if (!sword && sender != GuillotineSender) throw new InvalidOperationException("Unaudited local-effect sender.");
            Signature(sender, typeof(void), sword ? new[] { typeof(int), typeof(float) } :
                new[] { typeof(UnitAvatar), typeof(Vector3), typeof(float), typeof(bool) });
            var code = instructions.ToList();
            PlainBody(code, sender.Name);
            string identity = sword ? "System.Void Charm_IceSword::RpcCreateSwords(System.Int32,System.Single)" :
                "System.Void Charm_Guillotine::RpcCreateGuillotine(UnitAvatar,UnityEngine.Vector3,System.Single,System.Boolean)";
            string[] expected = sword ? new[] { "Get", "WriteVarInt", "WriteFloat", "SendRPCInternal", "Return" } :
                new[] { "Get", "WriteNetworkBehaviour", "WriteVector3", "WriteFloat", "WriteBool", "SendRPCInternal", "Return" };
            var send = AccessTools.DeclaredMethod(typeof(NetworkBehaviour), "SendRPCInternal");
            int call = SingleCall(code, send);
            if (call < 5 || code[call - 5].opcode != OpCodes.Ldstr || !Equals(code[call - 5].operand, identity) ||
                !code[call - 4].LoadsConstant(sword ? 960697688 : -992478868) ||
                !code[call - 2].LoadsConstant(0) || !code[call - 1].LoadsConstant(1) ||
                !code.Where(i => i.operand is MethodBase).Select(i => ((MethodBase)i.operand).Name).SequenceEqual(expected) ||
                code.Any(i => i.opcode.FlowControl == FlowControl.Branch || i.opcode.FlowControl == FlowControl.Cond_Branch ||
                    i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld))
                throw new InvalidOperationException("Native creation RPC contract changed: " + sender);
        }

        internal static void ValidateCallback(IEnumerable<CodeInstruction> instructions)
        {
            Signature(Callback, typeof(void), typeof(UnitAvatar), typeof(DamageInstance));
            Signature(Batch, typeof(void), typeof(Vector3), typeof(bool), typeof(bool), typeof(int), typeof(bool));
            var code = instructions.ToList();
            PlainBody(code, Callback.Name);
            int[] calls = Enumerable.Range(0, code.Count).Where(i => code[i].Calls(Batch)).ToArray();
            if (calls.Length != 2 || calls[1] < 4 || !code.Skip(calls[1] - 4).Take(4).All(i => i.LoadsConstant(0)))
                throw new InvalidOperationException("Native Frost callback call shape changed.");
            var frost = code.Skip(calls[0] + 1).Take(calls[1] - calls[0] - 1).ToList();
            foreach (string flag in new[] { "FLAMESWORDCALLBACKFROST", "FROSTRELIC" })
                if (frost.Count(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, flag)) != 1)
                    throw new InvalidOperationException("Native Frost callback eligibility changed: " + flag);
            foreach (string field in new[] { "isCooldown", "cooldownTimer" })
            {
                var member = AccessTools.Field(typeof(ComboEffect_FlameSword), field);
                if (member == null || code.Count(i => i.StoresField(member)) != 2 || frost.Count(i => i.StoresField(member)) != 1)
                    throw new InvalidOperationException("Native Frost callback cooldown changed: " + field);
            }
        }
        internal static IEnumerable<CodeInstruction> RewriteCallback(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            ValidateCallback(code);
            var call = code.Last(i => i.Calls(Batch));
            call.opcode = OpCodes.Call;
            call.operand = Local(nameof(FrostBatch));
            return code;
        }

        internal static void ValidateBatch(IEnumerable<CodeInstruction> instructions)
        {
            Signature(Batch, typeof(void), typeof(Vector3), typeof(bool), typeof(bool), typeof(int), typeof(bool));
            var code = instructions.ToList();
            PlainBody(code, Batch.Name);
            int call = SingleCall(code, Fire), auto = SingleCall(code, AutoSearch);
            if (auto <= call || call + 5 >= code.Count || code[call + 1].opcode != OpCodes.Pop ||
                !code.Skip(call + 2).Take(4).Any(i => i.LoadsConstant(1)) ||
                !code.Skip(call + 2).Take(4).Any(i => i.opcode == OpCodes.Add) ||
                !code.Take(call).Any(i => (i.opcode == OpCodes.Brtrue || i.opcode == OpCodes.Brtrue_S) &&
                    i.operand is Label label && code.Skip(call + 1).Any(target => target.labels.Contains(label))))
                throw new InvalidOperationException("Native Flame Sword synchronous loop changed.");
        }
        internal static IEnumerable<CodeInstruction> RewriteBatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            ValidateBatch(code);
            var frost = generator.DeclareLocal(typeof(bool));
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, Local(nameof(TakeBatchRequest)));
            yield return new CodeInstruction(OpCodes.Stloc, frost);
            foreach (var instruction in code)
            {
                if (!instruction.Calls(Fire)) { yield return instruction; continue; }
                var origin = new CodeInstruction(OpCodes.Ldloc, frost);
                origin.MoveLabelsFrom(instruction);
                yield return origin;
                yield return new CodeInstruction(OpCodes.Call, Local(nameof(BatchSword)));
            }
        }

        internal static void ValidateFireSword(IEnumerable<CodeInstruction> instructions)
        {
            Signature(Fire, typeof(bool), typeof(Vector3), typeof(Vector3));
            Signature(Use, typeof(bool), typeof(int));
            var code = instructions.ToList();
            PlainBody(code, Fire.Name);
            int use = SingleCall(code, Use), create = SingleCall(code, Create);
            if (use < 1 || create != use + 8 || create + 4 >= code.Count || !code[use - 1].LoadsConstant(1) ||
                !(code[use + 1].opcode == OpCodes.Brtrue || code[use + 1].opcode == OpCodes.Brtrue_S) ||
                !(code[use + 1].operand is Label success) || !code[use + 4].labels.Contains(success) ||
                !code[use + 2].LoadsConstant(0) || code[use + 3].opcode != OpCodes.Ret ||
                code[use + 4].opcode != OpCodes.Ldarg_0 || code[use + 5].opcode != OpCodes.Ldarg_0 ||
                code[use + 6].opcode != OpCodes.Ldarg_1 || code[use + 7].opcode != OpCodes.Ldarg_2 ||
                !(code[create + 1].operand is MethodInfo start) || start.Name != "StartCoroutine" ||
                code[create + 2].opcode != OpCodes.Pop || !code[create + 3].LoadsConstant(1) || code[create + 4].opcode != OpCodes.Ret)
                throw new InvalidOperationException("Native Flame Sword stock/coroutine continuation changed.");
        }
        internal static IEnumerable<CodeInstruction> RewriteFireSword(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            ValidateFireSword(code);
            int boundary = SingleCall(code, Use) + 4;
            var frost = generator.DeclareLocal(typeof(bool));
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, Local(nameof(TakeSwordRequest)));
            yield return new CodeInstruction(OpCodes.Stloc, frost);
            for (int i = 0; i < code.Count; i++)
            {
                var instruction = code[i];
                if (i == boundary)
                {
                    Label accepted = generator.DefineLabel();
                    var origin = new CodeInstruction(OpCodes.Ldloc, frost);
                    origin.MoveLabelsFrom(instruction);
                    yield return origin;
                    yield return new CodeInstruction(OpCodes.Brfalse, accepted);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, Local(nameof(AdmitFlameSword)));
                    yield return new CodeInstruction(OpCodes.Brtrue, accepted);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Ret);
                    instruction.labels.Add(accepted);
                }
                yield return instruction;
            }
        }
    }
}
