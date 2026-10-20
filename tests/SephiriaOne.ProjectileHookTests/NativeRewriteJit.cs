using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;

internal static class NativeRewriteJit
{
    // Compile copies of the complete rewritten native methods, but never invoke them.
    // This catches type/stack/branch errors without starting Unity or detouring the game.
    internal static void Verify(MethodInfo original,
        Func<List<CodeInstruction>, ILGenerator, List<CodeInstruction>> rewrite)
    {
        Type[] parameters = (original.IsStatic ? Type.EmptyTypes : new[] { original.DeclaringType! })
            .Concat(original.GetParameters().Select(p => p.ParameterType)).ToArray();
        var method = new DynamicMethod("Audit_" + original.DeclaringType!.Name + "_" + original.Name,
            original.ReturnType, parameters, original.Module, true);
        var generator = method.GetILGenerator();
        var code = rewrite(PatchProcessor.GetOriginalInstructions(original, generator).ToList(), generator);
        foreach (var instruction in code)
        {
            foreach (Label label in instruction.labels) generator.MarkLabel(label);
            foreach (var block in instruction.blocks)
                switch (block.blockType)
                {
                    case ExceptionBlockType.BeginExceptionBlock: generator.BeginExceptionBlock(); break;
                    case ExceptionBlockType.BeginCatchBlock: generator.BeginCatchBlock(block.catchType); break;
                    case ExceptionBlockType.BeginExceptFilterBlock: generator.BeginExceptFilterBlock(); break;
                    case ExceptionBlockType.BeginFaultBlock: generator.BeginFaultBlock(); break;
                    case ExceptionBlockType.BeginFinallyBlock: generator.BeginFinallyBlock(); break;
                }
            Emit(generator, LongBranch(instruction.opcode), instruction.operand);
            foreach (var block in instruction.blocks)
                if (block.blockType == ExceptionBlockType.EndExceptionBlock) generator.EndExceptionBlock();
        }
        Type delegateType = Expression.GetDelegateType(parameters.Append(original.ReturnType).ToArray());
        var compiled = method.CreateDelegate(delegateType);
        RuntimeHelpers.PrepareDelegate(compiled);
        GC.KeepAlive(compiled);
    }

    private static OpCode LongBranch(OpCode opcode)
    {
        if (opcode.OperandType != OperandType.ShortInlineBrTarget) return opcode;
        return opcode.Value switch
        {
            var value when value == OpCodes.Br_S.Value => OpCodes.Br,
            var value when value == OpCodes.Brfalse_S.Value => OpCodes.Brfalse,
            var value when value == OpCodes.Brtrue_S.Value => OpCodes.Brtrue,
            var value when value == OpCodes.Beq_S.Value => OpCodes.Beq,
            var value when value == OpCodes.Bge_S.Value => OpCodes.Bge,
            var value when value == OpCodes.Bge_Un_S.Value => OpCodes.Bge_Un,
            var value when value == OpCodes.Bgt_S.Value => OpCodes.Bgt,
            var value when value == OpCodes.Bgt_Un_S.Value => OpCodes.Bgt_Un,
            var value when value == OpCodes.Ble_S.Value => OpCodes.Ble,
            var value when value == OpCodes.Ble_Un_S.Value => OpCodes.Ble_Un,
            var value when value == OpCodes.Blt_S.Value => OpCodes.Blt,
            var value when value == OpCodes.Blt_Un_S.Value => OpCodes.Blt_Un,
            var value when value == OpCodes.Bne_Un_S.Value => OpCodes.Bne_Un,
            var value when value == OpCodes.Leave_S.Value => OpCodes.Leave,
            _ => throw new Exception("Unsupported short branch: " + opcode)
        };
    }

    private static void Emit(ILGenerator il, OpCode opcode, object? operand)
    {
        switch (operand)
        {
            case null: il.Emit(opcode); break;
            case Label label: il.Emit(opcode, label); break;
            case Label[] labels: il.Emit(opcode, labels); break;
            case LocalBuilder local: il.Emit(opcode, local); break;
            case FieldInfo field: il.Emit(opcode, field); break;
            case MethodInfo method: il.Emit(opcode, method); break;
            case ConstructorInfo constructor: il.Emit(opcode, constructor); break;
            case Type type: il.Emit(opcode, type); break;
            case string text: il.Emit(opcode, text); break;
            case sbyte value: il.Emit(opcode, value); break;
            case byte value: il.Emit(opcode, value); break;
            case short value: il.Emit(opcode, value); break;
            case int value: il.Emit(opcode, value); break;
            case long value: il.Emit(opcode, value); break;
            case float value: il.Emit(opcode, value); break;
            case double value: il.Emit(opcode, value); break;
            default: throw new Exception("Unsupported IL operand " + operand.GetType().Name + " for " + opcode);
        }
    }
}
