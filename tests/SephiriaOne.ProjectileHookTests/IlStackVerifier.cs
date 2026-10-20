using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class IlStackVerifier
{
    internal static void Verify(MethodInfo method, IReadOnlyList<CodeInstruction> code)
    {
        var labels = new Dictionary<Label, int>();
        for (int i = 0; i < code.Count; i++)
            foreach (Label label in code[i].labels)
                if (!labels.TryAdd(label, i)) throw new Exception($"Duplicate IL label at {method}, instruction {i}");
        var heights = new Dictionary<int, int>();
        var work = new Queue<(int index, int height)>();
        work.Enqueue((0, 0));
        for (int i = 0; i < code.Count; i++)
            foreach (var block in code[i].blocks)
                if (block.blockType is ExceptionBlockType.BeginCatchBlock or ExceptionBlockType.BeginExceptFilterBlock)
                    work.Enqueue((i, 1));
                else if (block.blockType is ExceptionBlockType.BeginFinallyBlock or ExceptionBlockType.BeginFaultBlock)
                    work.Enqueue((i, 0));
        while (work.TryDequeue(out var state))
        {
            if (state.index < 0 || state.index >= code.Count) throw new Exception($"IL falls outside {method}");
            if (heights.TryGetValue(state.index, out int previous))
            {
                if (previous != state.height)
                    throw new Exception($"Stack merge mismatch at {method}, instruction {state.index}: {previous} vs {state.height}");
                continue;
            }
            heights.Add(state.index, state.height);
            var instruction = code[state.index];
            int pop = PopCount(instruction, method);
            int push = PushCount(instruction);
            if (state.height < pop) throw new Exception($"Stack underflow at {method}, instruction {state.index}: {instruction}");
            int height = state.height - pop + push;
            if (instruction.opcode == OpCodes.Leave || instruction.opcode == OpCodes.Leave_S) height = 0;
            if (instruction.opcode.FlowControl is FlowControl.Return or FlowControl.Throw)
            {
                if (height != 0) throw new Exception($"Nonempty terminal stack at {method}, instruction {state.index}");
                continue;
            }
            if (instruction.operand is Label target) work.Enqueue((labels[target], height));
            else if (instruction.operand is Label[] targets)
                foreach (var destination in targets) work.Enqueue((labels[destination], height));
            if (instruction.opcode.FlowControl != FlowControl.Branch) work.Enqueue((state.index + 1, height));
        }
    }

    private static int PopCount(CodeInstruction instruction, MethodInfo original)
    {
        if (instruction.opcode.StackBehaviourPop != StackBehaviour.Varpop)
            return FixedCount(instruction.opcode.StackBehaviourPop);
        if (instruction.opcode == OpCodes.Ret) return original.ReturnType == typeof(void) ? 0 : 1;
        if (instruction.operand is MethodBase called)
            return called.GetParameters().Length + (instruction.opcode == OpCodes.Newobj || called.IsStatic ? 0 : 1);
        throw new Exception("Unsupported variable stack pop: " + instruction);
    }

    private static int PushCount(CodeInstruction instruction)
    {
        if (instruction.opcode.StackBehaviourPush != StackBehaviour.Varpush)
            return FixedCount(instruction.opcode.StackBehaviourPush);
        if (instruction.operand is MethodInfo called) return called.ReturnType == typeof(void) ? 0 : 1;
        throw new Exception("Unsupported variable stack push: " + instruction);
    }

    private static int FixedCount(StackBehaviour behavior)
    {
        if (behavior is StackBehaviour.Pop0 or StackBehaviour.Push0) return 0;
        return behavior.ToString().Count(c => c == '_') + 1;
    }
}
