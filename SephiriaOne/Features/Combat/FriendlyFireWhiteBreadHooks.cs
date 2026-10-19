using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaOne
{
    internal static class FriendlyFireWhiteBreadHooks
    {
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Select(i => new CodeInstruction(i)).ToList();
            var enabled = Field("isHitInvincibleEnabled", typeof(bool));
            var elapsed = Field("invincibleTimer", typeof(float));
            var kind = Field("invincibleType", typeof(EInvincibleType));
            var fall = Field("restoreFallingTimer", typeof(Timer));
            var reads = Enumerable.Range(0, code.Count).Where(i => code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, enabled)).ToArray();
            int read = reads.Length == 1 ? reads[0] : -1;
            if (read <= 0 || read + 1 >= code.Count || code[read - 1].opcode != OpCodes.Ldarg_0 ||
                (code[read + 1].opcode != OpCodes.Brfalse && code[read + 1].opcode != OpCodes.Brfalse_S))
                throw new InvalidOperationException("Native hit-invulnerability admission changed.");
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.MoveLabelsFrom(code[read + 1]); load.MoveBlocksFrom(code[read + 1]);
            code.InsertRange(read + 1, new[]
            {
                load,
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, elapsed),
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, kind),
                new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, fall),
                CodeInstruction.Call(typeof(PvpWhiteBread), nameof(PvpWhiteBread.IsHitInvincible))
            });
            return code;
        }

        private static FieldInfo Field(string name, Type type)
        {
            var field = AccessTools.DeclaredField(typeof(UnitAvatar), name);
            if (field == null || field.IsStatic || field.FieldType != type)
                throw new InvalidOperationException("Native hit-invulnerability field changed: " + name);
            return field;
        }
    }
}
