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
    // Optional cosmetic broadcast. Native observers (including the host) render
    // exactly once; no local spawn, gameplay callback, player state or retry loop.
    internal static class NativePlayerFloatingText
    {
        private static bool checkedTransport, warned;
        private static Action<UnitAvatar, Vector2, string, Color, int, bool, UnitAvatar, UnitAvatar> send;

        internal static void Reset() { checkedTransport = warned = false; send = null; }

        internal static void Show(PlayerAvatar player, string message, bool emphasize = false)
        {
            try
            {
                if (!NetworkServer.active || !player || !player.isServer || player.netId == 0 ||
                    !player.spawner || !ReferenceEquals(player.spawner.PlayerAvatar, player)) return;
                var connection = player.spawner.connectionToClient;
                if (connection == null || !connection.isReady || !ReferenceEquals(player.connectionToClient, connection) ||
                    !ReferenceEquals(connection.identity, player.netIdentity) ||
                    !NetworkServer.connections.TryGetValue(connection.connectionId, out var live) || !ReferenceEquals(live, connection) ||
                    !NetworkServer.spawned.TryGetValue(player.netId, out var identity) || !ReferenceEquals(identity, player.netIdentity)) return;
                Vector3 position = player.transform.position;
                float height = player.TopdownActor.size.y + 0.25f;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z) || !Finite(height) || !Finite(position.y + height)) return;
                string text = ChatTextSanitizer.Sanitize(message, 120);
                if (string.IsNullOrWhiteSpace(text)) return;
                // The stock prefab enables TMP rich text but uses a bitmap font.
                // Modest enlargement makes emphasis visible even when synthetic
                // bold weight has little effect. Add only trusted tags AFTER
                // sanitizing the localized text; do not mutate the shared font.
                if (emphasize) text = "<b><size=125%>" + text + "</size></b>";
                if (!checkedTransport)
                {
                    checkedTransport = true;
                    if (!Validate()) throw new InvalidOperationException("Native floating-text broadcast contract changed.");
                    send = (Action<UnitAvatar, Vector2, string, Color, int, bool, UnitAvatar, UnitAvatar>)Delegate.CreateDelegate(
                        typeof(Action<UnitAvatar, Vector2, string, Color, int, bool, UnitAvatar, UnitAvatar>),
                        AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcShowDamageParticle"));
                }
                // Keep the stock unreliable broadcast and owner inclusion. Normal
                // prefab preserves cyan (the large native variant forces red).
                // isPrivate=false bypasses damage-display
                // preferences, not network audience selection. Dead avatars are valid.
                send?.Invoke(player, new Vector2(position.x, position.y + height), text, Color.cyan, 0, false, null, null);
            }
            catch (Exception error)
            {
                send = null;
                if (warned) return;
                warned = true;
                Debug.LogWarning("[SephiriaOne] Floating countdown unavailable; chat and gameplay remain active: " + error);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool Validate()
        {
            var sender = AccessTools.DeclaredMethod(typeof(UnitAvatar), "RpcShowDamageParticle",
                new[] { typeof(Vector2), typeof(string), typeof(Color), typeof(int), typeof(bool), typeof(UnitAvatar), typeof(UnitAvatar) });
            var receive = AccessTools.DeclaredMethod(typeof(UnitAvatar), "InvokeUserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar");
            var body = AccessTools.DeclaredMethod(typeof(UnitAvatar), "UserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar");
            return sender != null && !sender.IsStatic && sender.ReturnType == typeof(void) && receive != null && body != null &&
                ValidateCode(PatchProcessor.GetOriginalInstructions(sender), PatchProcessor.GetOriginalInstructions(receive),
                    PatchProcessor.GetOriginalInstructions(body));
        }

        internal static bool ValidateCode(IEnumerable<CodeInstruction> sendCode, IEnumerable<CodeInstruction> receiveCode,
            IEnumerable<CodeInstruction> bodyCode)
        {
            var sender = sendCode.Where(i => i.opcode != OpCodes.Nop).ToList();
            var receiver = receiveCode.ToList();
            var body = bodyCode.ToList();
            int dispatch = sender.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            // Share the exact native payload check with Rabbit's existing private
            // low-MP alert, then validate the broadcast and cosmetic-only receiver.
            return NativePlayerAlert.ValidateCode(sender, receiver) &&
                dispatch >= 2 && sender[dispatch - 2].LoadsConstant(1) && sender[dispatch - 1].LoadsConstant(1) &&
                Calls(sender).Select(m => m.Name).SequenceEqual(new[] { "Get", "WriteVector2", "WriteString", "WriteColor", "WriteVarInt",
                    "WriteBool", "WriteNetworkBehaviour", "WriteNetworkBehaviour", "SendRPCInternal", "Return" }) &&
                Calls(receiver).Select(m => m.Name).SequenceEqual(new[] { "get_active", "LogError", "ReadVector2", "ReadString", "ReadColor",
                    "ReadVarInt", "ReadBool", "ReadNetworkBehaviour", "ReadNetworkBehaviour",
                    "UserCode_RpcShowDamageParticle__Vector2__String__Color__Int32__Boolean__UnitAvatar__UnitAvatar" }) &&
                !body.Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli || i.opcode == OpCodes.Newobj) &&
                Calls(body).Select(m => m.Name).SequenceEqual(new[] { "op_Implicit", "op_Implicit", "get_Instance", "get_Options", "GetBool",
                    "get_isLocalPlayer", "get_isLocalPlayer", "get_NetworkLeader", "op_Implicit", "get_NetworkLeader", "get_isLocalPlayer",
                    "get_NetworkLeader", "op_Implicit", "get_NetworkLeader", "get_isLocalPlayer", "get_Pool", "get_insideUnitCircle", "op_Addition",
                    "op_Implicit", "Spawn", "get_red", "get_Pool", "get_insideUnitCircle", "op_Addition", "op_Implicit", "Spawn", "SetDamage" }) &&
                Calls(body).Where(m => m.Name == "Spawn").All(m => m.IsGenericMethod &&
                    (m.GetGenericArguments().SequenceEqual(new[] { typeof(UI_DamageParticle) }) ||
                     m.GetGenericArguments().SequenceEqual(new[] { typeof(UI_DamageParticle_Player) })));
        }

        private static IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code
            .Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt).Select(i => i.operand).OfType<MethodInfo>();
    }
}
