using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    internal static class DeathmatchFeature
    {
        internal static bool Available { get; private set; }
        internal static void Initialize()
        {
            try { HarmonyRuntime.EnsureLoaded(); Validate(); Available = true; }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Deathmatch chat unavailable: " + error); }
        }
        [MethodImpl(MethodImplOptions.NoInlining)] internal static void Validate()
        {
            var receiver = AccessTools.DeclaredMethod(typeof(DungeonManager), "UserCode_RpcChat__PlayerAvatar__String__String");
            var bubble = AccessTools.DeclaredMethod(typeof(PlayerAvatar), "CreateChatBubble", new[] { typeof(string) });
            if (receiver == null || bubble == null ||
                !PatchProcessor.GetOriginalInstructions(receiver).Any(i => Equals(i.operand, bubble)) ||
                PatchProcessor.GetOriginalInstructions(bubble).Any(i => i.operand is FieldInfo f && f.Name == "IsDead"))
                throw new InvalidOperationException("Native chat bubble/dead-player contract changed.");
        }
        internal static void Shutdown()
        {
            DeathmatchRuntime.Stop(true, false);
            if (DeathmatchRuntime.RecoveryPending)
                throw new InvalidOperationException("Deathmatch recovery is finishing a native callback; retry unload after it completes.");
            Available = false;
        }
    }
}
