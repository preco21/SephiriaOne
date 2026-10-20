using System.Reflection.Emit;
using HarmonyLib;

internal static class GuardExecution
{
    // Execute only the injected guard instructions; native Unity calls are never emitted.
    internal static int Run(IReadOnlyList<CodeInstruction> guard, object? result)
    {
        var method = new DynamicMethod("ProjectileNullGuard", typeof(int), new[] { typeof(object) });
        var il = method.GetILGenerator();
        var counter = il.DeclareLocal(typeof(int));
        Label accepted = il.DefineLabel(), continuation = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        foreach (var instruction in guard)
        {
            if (instruction.opcode == OpCodes.Brtrue) il.Emit(instruction.opcode, accepted);
            else if (instruction.opcode == OpCodes.Br) il.Emit(instruction.opcode, continuation);
            else il.Emit(instruction.opcode);
        }
        il.MarkLabel(accepted);
        il.Emit(OpCodes.Pop); // Native configuration consumes its non-null projectile.
        il.Emit(OpCodes.Ldc_I4, 10);
        il.Emit(OpCodes.Stloc, counter);
        il.MarkLabel(continuation);
        il.Emit(OpCodes.Ldloc, counter);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Add);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<object?, int>>()(result);
    }
}
