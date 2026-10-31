using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

internal static class GamePresentationAudioTests
{
    internal static void Run(Assembly game)
    {
        Type sound = game.GetType("SoundManager", true)!;
        MethodInfo? create = AccessTools.DeclaredMethod(sound, "TryCreateSoundInstance");
        if (create == null) return; // Stable build calls FMOD directly.
        void Require(bool ok, string why) { if (!ok) throw new Exception("Presentation audio wrapper: " + why); }
        List<CodeInstruction> Code(MethodInfo method) => PatchProcessor.GetOriginalInstructions(method).ToList();
        IEnumerable<MethodInfo> Calls(List<CodeInstruction> code) => code.Select(i => i.operand).OfType<MethodInfo>();
        var emitter = AccessTools.DeclaredMethod(sound, "PlaySoundEmitter");
        var shot = sound.GetMethods().Single(m => m.Name == "PlayOneShot" && m.GetParameters()[0].ParameterType.FullName == "FMODUnity.EventReference");
        foreach (var method in new[] { create, emitter, shot })
        {
            var code = Code(method);
            Require(Calls(code).All(m =>
                m.DeclaringType?.FullName is "FMODUnity.RuntimeManager" or "FMODUnity.StudioEventEmitter" or
                    "FMODUnity.EventReference" or "FMOD.Studio.EventInstance" or "UnityEngine.Object" or "UnityEngine.Debug" ||
                m.DeclaringType == typeof(string) || m.DeclaringType == typeof(Exception) ||
                m.DeclaringType?.IsGenericType == true && m.DeclaringType.GetGenericTypeDefinition() == typeof(HashSet<>)),
                method.Name + " forwards only audio and diagnostic calls");
            Require(!code.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Calli) &&
                code.Where(i => i.opcode == OpCodes.Stsfld).All(i => i.operand is FieldInfo f &&
                    f.DeclaringType == sound && f.Name == "warnedInitializationFailure"),
                method.Name + " writes only its warning latch");
            Require(method.GetMethodBody()!.ExceptionHandlingClauses.All(c => c.Flags == ExceptionHandlingClauseOptions.Clause &&
                c.CatchType?.FullName is "FMODUnity.EventNotFoundException" or "FMODUnity.SystemNotInitializedException"),
                method.Name + " catches only documented audio failures");
        }
        Require(Calls(Code(create)).Count(m => m.DeclaringType?.FullName == "FMODUnity.RuntimeManager" && m.Name == "CreateInstance") == 1 &&
            Calls(Code(create)).Count(m => m.DeclaringType?.FullName == "FMOD.Studio.EventInstance" && m.Name == "isValid") == 1,
            "instance factory forwards once and returns validity without starting playback");
        Require(Calls(Code(emitter)).Count(m => m.DeclaringType?.FullName == "FMODUnity.StudioEventEmitter" && m.Name == "Play") == 1,
            "emitter wrapper forwards once");
        var shotCode = Code(shot);
        int play = shotCode.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType?.FullName == "FMODUnity.RuntimeManager" && m.Name == "PlayOneShot");
        Require(play >= 2 && shotCode[play - 2].opcode == OpCodes.Ldarg_0 && shotCode[play - 1].opcode == OpCodes.Ldarg_1,
            "one-shot wrapper preserves event and world position");
        Console.WriteLine("Verified beta SoundManager presentation forwarding, audio-only dependencies and bounded diagnostic exception handling (not live audio).");
    }
}
