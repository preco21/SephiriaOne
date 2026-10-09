using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class ReviveAllHooks
    {
        private const string Id = "SephiriaOne.ReviveAll";
        internal static void Install()
        {
            var method = AccessTools.DeclaredMethod(typeof(UnitAvatar), "Revive", new[] { typeof(float) });
            if (method == null) throw new InvalidOperationException("Native revival method missing.");
            Rewrite(PatchProcessor.GetOriginalInstructions(method)).ToList();
            var harmony = new Harmony(Id);
            try { harmony.Patch(method, transpiler: new HarmonyMethod(typeof(ReviveAllHooks), nameof(Rewrite))); }
            catch { harmony.UnpatchAll(Id); throw; }
        }
        internal static void Uninstall() => new Harmony(Id).UnpatchAll(Id);

        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            bool Call(CodeInstruction i, Type type, string name) => (i.opcode == OpCodes.Callvirt || i.opcode == OpCodes.Call) &&
                i.operand is MethodInfo m && m.DeclaringType == type && m.Name == name;
            int hp = code.FindIndex(i => Call(i, typeof(Action<float>), "Invoke"));
            int revived = code.FindIndex(i => Call(i, typeof(Action), "Invoke"));
            int invulnerable = code.FindIndex(i => Call(i, typeof(UnitAvatar), "StartReviveInvulnerable"));
            int inventory = code.FindIndex(i => Call(i, typeof(UnitAvatar), "TakeRemoteInventory"));
            int rpc = code.FindIndex(i => Call(i, typeof(UnitAvatar), "RpcRevive"));
            if (!(hp >= 0 && revived > hp && invulnerable > revived && inventory > invulnerable && rpc > inventory) ||
                code.Count(i => Call(i, typeof(Action<float>), "Invoke")) != 1 || code.Count(i => Call(i, typeof(Action), "Invoke")) != 1 ||
                code.Count(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.DeclaringType == typeof(UnitAvatar) && f.Name == "OnHpChangedServerside") != 1 ||
                code.Count(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f.DeclaringType == typeof(UnitAvatar) && f.Name == "OnRevive") != 1)
                throw new InvalidOperationException("Native revival callback/tail contract changed.");
            Replace(revived, nameof(InvokeRevived)); Replace(hp, nameof(InvokeHpChanged));
            return code;

            void Replace(int index, string helper)
            {
                var target = code[index];
                var load = new CodeInstruction(OpCodes.Ldarg_0);
                load.labels.AddRange(target.labels); target.labels.Clear();
                load.blocks.AddRange(target.blocks); target.blocks.Clear();
                target.opcode = OpCodes.Call; target.operand = AccessTools.Method(typeof(ReviveAllHooks), helper);
                code.Insert(index, load);
            }
        }

        internal static void InvokeHpChanged(Action<float> callbacks, float hp, UnitAvatar player)
        {
            if (!ReviveAllAction.IsRecovering(player)) { callbacks(hp); return; }
            foreach (Action<float> callback in callbacks.GetInvocationList())
            {
                ReviveAllAction.CheckScope();
                try { callback(hp); } catch (Exception error) { ReviveAllAction.CallbackFailed(error); }
                ReviveAllAction.CheckScope();
            }
        }
        internal static void InvokeRevived(Action callbacks, UnitAvatar player)
        {
            if (!ReviveAllAction.IsRecovering(player)) { callbacks(); return; }
            foreach (Action callback in callbacks.GetInvocationList())
            {
                ReviveAllAction.CheckScope();
                try { callback(); } catch (Exception error) { ReviveAllAction.CallbackFailed(error); }
                ReviveAllAction.CheckScope();
            }
        }
    }
}
