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
    internal static class FriendlyFireDarkCloudVisualHooks
    {
        private const string Id = "SephiriaOne.DarkCloudVisuals";
        private const string Body = "UserCode_TargetCreateFx__NetworkConnectionToClient__Vector3__Boolean__Boolean";
        private const string Rpc = "System.Void ComboEffect_DarkCloud::TargetCreateFx(Mirror.NetworkConnectionToClient,UnityEngine.Vector3,System.Boolean,System.Boolean)";
        internal static void TryInstall()
        {
            Uninstall();
            try
            {
                if (!Validate()) throw new InvalidOperationException("Native Dark Cloud cosmetic contract changed.");
                var type = typeof(ComboEffect_DarkCloud);
                var sender = AccessTools.DeclaredMethod(type, "TargetCreateFx");
                FriendlyFireDarkCloudVisuals.send = AccessTools.MethodDelegate<Action<ComboEffect_DarkCloud, NetworkConnectionToClient, Vector3, bool, bool>>(sender);
                var harmony = new Harmony(Id);
                harmony.Patch(AccessTools.DeclaredMethod(type, "FireLightning"),
                    prefix: new HarmonyMethod(typeof(FriendlyFireDarkCloudVisuals), nameof(FriendlyFireDarkCloudVisuals.BeforeStrike)),
                    finalizer: new HarmonyMethod(typeof(FriendlyFireDarkCloudVisuals), nameof(FriendlyFireDarkCloudVisuals.AfterStrike)));
                harmony.Patch(sender, postfix: new HarmonyMethod(typeof(FriendlyFireDarkCloudVisuals), nameof(FriendlyFireDarkCloudVisuals.AfterNativeSend)));
            }
            catch (Exception error) { Uninstall(); FriendlyFireDarkCloudVisuals.Warn(error); }
        }

        internal static void Uninstall() { new Harmony(Id).UnpatchAll(Id); FriendlyFireDarkCloudVisuals.Reset(); }

        internal static bool Validate()
        {
            var type = typeof(ComboEffect_DarkCloud);
            var sender = AccessTools.DeclaredMethod(type, "TargetCreateFx",
                new[] { typeof(NetworkConnectionToClient), typeof(Vector3), typeof(bool), typeof(bool) });
            var receiver = AccessTools.DeclaredMethod(type, "Invoke" + Body);
            var body = AccessTools.DeclaredMethod(type, Body);
            var start = AccessTools.DeclaredMethod(type, "OnStartClient");
            var visual = type.Assembly.GetType("DarkCloudObject");
            var attack = visual == null ? null : AccessTools.DeclaredMethod(visual, "Attack");
            if (sender == null || sender.IsStatic || sender.ReturnType != typeof(void) || receiver == null || body == null || start == null || attack == null) return false;
            var startup = PatchProcessor.GetOriginalInstructions(start);
            int created = startup.FindIndex(i => i.opcode == OpCodes.Stfld && i.operand is FieldInfo f && f.Name == "darkCloudObject");
            int ownerGate = startup.FindIndex(i => i.operand is MethodInfo m && m.Name == "get_isOwned");
            return created >= 0 && ownerGate > created &&
                Calls(PatchProcessor.GetOriginalInstructions(attack)).Select(m => m.Name)
                    .SequenceEqual(new[] { "Range", "SetValue", "SetValue", "SetValue", "Play" }) &&
                ValidateCode(PatchProcessor.GetOriginalInstructions(sender), PatchProcessor.GetOriginalInstructions(receiver), PatchProcessor.GetOriginalInstructions(body));
        }

        internal static bool ValidateCode(IEnumerable<CodeInstruction> sender, IEnumerable<CodeInstruction> receiver, IEnumerable<CodeInstruction> body)
        {
            var code = sender.ToList();
            int dispatch = code.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendTargetRPCInternal");
            var cosmetic = body.ToList();
            var allowed = new HashSet<string> { "op_Implicit", "get_transform", "get_position", "op_Subtraction", "get_Pool",
                "Concat", "Spawn", "GetAngleFromVector", "SetRotation", "get_magnitude", "SetSizeY", "Range", "Attack" };
            return dispatch > 0 && code[dispatch - 1].LoadsConstant(0) &&
                code.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, Rpc)) && code.Any(i => i.LoadsConstant(-1744830929)) &&
                Calls(code).Select(m => m.Name).SequenceEqual(new[] { "Get", "WriteVector3", "WriteBool", "WriteBool", "SendTargetRPCInternal", "Return" }) &&
                Calls(receiver).Select(m => m.Name).SequenceEqual(new[] { "get_active", "LogError", "ReadVector3", "ReadBool", "ReadBool", Body }) &&
                !cosmetic.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli) &&
                Calls(cosmetic).All(m => allowed.Contains(m.Name)) &&
                Calls(cosmetic).Count(m => m.DeclaringType?.Name == "DarkCloudObject" && m.Name == "Attack") == 1 &&
                new[] { "LightningFx", "IceLightningFx", "LightningHeadFx", "IceLightningHeadFx", "LightningTailFx", "IceLightningTailFx", "_EX", "IceLightningHeadFx_Plus" }
                    .All(key => cosmetic.Any(i => i.opcode == OpCodes.Ldstr && Equals(i.operand, key)));
        }
        private static IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code
            .Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt).Select(i => i.operand).OfType<MethodInfo>();
    }
}
