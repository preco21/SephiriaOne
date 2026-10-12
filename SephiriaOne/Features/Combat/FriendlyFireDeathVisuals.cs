using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace SephiriaOne
{
    // Broadcast only a native impact animation. No projectile, damage, fake
    // death, casting state or guest addon protocol participates in this path.
    internal static class FriendlyFireDeathVisuals
    {
        private const string BulletName = "PlayerDefaultBigBullet";
        private const string EffectName = "PlayerDefaultBigBulletDestroyFx";
        private const float Scale = 4f;
        private const string Rpc = "System.Void DungeonManager::RpcBulletDestroyed(System.UInt32,System.Boolean,System.String,UnityEngine.Vector3,System.Single,System.Single,System.Single)";
        private const int Hash = -1283831235;
        private static bool initialized, available, warned;

        internal static void Reset() { initialized = available = warned = false; }

        internal static void Show(PlayerAvatar player)
        {
            try
            {
                var dungeon = DungeonManager.Instance;
                if (!Mirror.NetworkServer.active || !player || !player.isServer || !player.IsDead ||
                    !dungeon || !dungeon.isServer) return;
                if (!initialized)
                {
                    initialized = true;
                    if (!Validate()) throw new InvalidOperationException("Native death visual transport changed.");
                    available = true;
                }
                if (!available) return;
                // Recheck current assets/pools on each death, never retain a
                // previous run's initialized prefab or any scene object.
                var bullet = Bullet.Pool?.GetPrefab(BulletName);
                var module = bullet ? bullet.DestroyModule : null;
                var prefab = module ? module.destroyFxPrefab : null;
                var fx = prefab ? prefab.GetComponent<SpriteFx>() : null;
                // Loading/teardown may temporarily retire pools. Skip this
                // transient visual without disabling later runs' indicators.
                if (!module || !prefab || !fx || SpriteFx.Pool == null ||
                    !SpriteFx.Pool.pooledGameObjects.ContainsKey(prefab)) return;
                if (module.GetType() != typeof(BulletDestroyModule) || !module.destroySound.IsNull ||
                    module.destroyFxCount != 1 || module.destroyFxErrorRadius != 0 || module.randomFxAngleError != 0 ||
                    module.randomFxScaleX || module.lookAtBulletDirection || module.destroyFxPrefabList.Count != 0 ||
                    module.breakFxFragments.Count != 0 || prefab.name != EffectName ||
                    fx.GetType() != typeof(SpriteFx) || !fx.automaticallyDespawnOnExit || fx.overrideAnimationTransition)
                    throw new InvalidOperationException("Native red impact FX prefab unavailable or changed.");
                Vector3 position = player.transform.position;
                float height = player.TopdownActor ? player.TopdownActor.CenterYPos : 0f;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z) || !Finite(height)) return;
                // false bypasses party-effects transparency. The owner retains
                // their native crash plus this smaller, separate red burst.
                dungeon.BroadcastDestroyBullet(player.netId, false, BulletName, position, height, 0f, Scale);
            }
            catch (Exception error)
            {
                available = false;
                if (warned) return;
                warned = true;
                Debug.LogWarning("[SephiriaOne] Multiplayer death FX unavailable; combat and recovery remain active: " + error);
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool Validate()
        {
            var broadcast = AccessTools.DeclaredMethod(typeof(DungeonManager), "BroadcastDestroyBullet");
            var sender = AccessTools.DeclaredMethod(typeof(DungeonManager), "RpcBulletDestroyed");
            var receiver = AccessTools.DeclaredMethod(typeof(DungeonManager), "InvokeUserCode_RpcBulletDestroyed__UInt32__Boolean__String__Vector3__Single__Single__Single");
            var body = AccessTools.DeclaredMethod(typeof(DungeonManager), "UserCode_RpcBulletDestroyed__UInt32__Boolean__String__Vector3__Single__Single__Single");
            var visual = AccessTools.DeclaredMethod(typeof(BulletDestroyModule), "CreateDestroyVisualOnClient");
            return broadcast != null && sender != null && receiver != null && body != null && visual != null &&
                Calls(PatchProcessor.GetOriginalInstructions(broadcast)).Select(m => m.Name)
                    .SequenceEqual(new[] { "get_active", "LogWarning", "RpcBulletDestroyed" }) &&
                ValidateCode(PatchProcessor.GetOriginalInstructions(sender), PatchProcessor.GetOriginalInstructions(receiver),
                    PatchProcessor.GetOriginalInstructions(body), PatchProcessor.GetOriginalInstructions(visual));
        }

        internal static bool ValidateCode(IEnumerable<CodeInstruction> sender, IEnumerable<CodeInstruction> receiver,
            IEnumerable<CodeInstruction> body, IEnumerable<CodeInstruction> visual)
        {
            var code = sender.Where(i => i.opcode != OpCodes.Nop).ToList();
            int dispatch = code.FindIndex(i => i.operand is MethodInfo m && m.Name == "SendRPCInternal");
            if (!Calls(code).Select(m => m.Name).SequenceEqual(new[] { "Get", "WriteVarUInt", "WriteBool", "WriteString", "WriteVector3", "WriteFloat", "WriteFloat", "WriteFloat", "SendRPCInternal", "Return" }) ||
                code.Count(i => i.LoadsConstant(Rpc)) != 1 || code.Count(i => i.LoadsConstant(Hash)) != 1 ||
                dispatch < 2 || !code[dispatch - 2].LoadsConstant(0) || !code[dispatch - 1].LoadsConstant(1)) return false;
            if (!Calls(receiver).Select(m => m.Name).SequenceEqual(new[] { "get_active", "LogError", "ReadVarUInt", "ReadBool", "ReadString", "ReadVector3", "ReadFloat", "ReadFloat", "ReadFloat",
                "UserCode_RpcBulletDestroyed__UInt32__Boolean__String__Vector3__Single__Single__Single" })) return false;
            if (!Calls(body).Select(m => m.Name).SequenceEqual(new[] { "get_Pool", "get_Pool", "GetPrefab", "op_Implicit", "op_Implicit", "CreateDestroyVisualOnClient" })) return false;
            var fx = visual.ToList();
            // Selected module has no sound, alternate FX or fragments and cannot
            // dispatch a gameplay override. Reject new writes/non-cosmetic calls.
            return fx.Count(i => i.operand is MethodInfo m && m.Name == "Spawn" && m.ReturnType == typeof(SpriteFx)) == 1 &&
                !body.Concat(fx).Any(i => i.opcode == OpCodes.Stfld || i.opcode == OpCodes.Stsfld || i.opcode == OpCodes.Calli) &&
                fx.Where(i => i.operand is ConstructorInfo).All(i => ((ConstructorInfo)i.operand).DeclaringType == typeof(Vector3)) &&
                Calls(fx).All(AllowedVisualCall);
        }

        private static IEnumerable<MethodInfo> Calls(IEnumerable<CodeInstruction> code) => code
            .Where(i => i.opcode == OpCodes.Call || i.opcode == OpCodes.Callvirt).Select(i => i.operand).OfType<MethodInfo>();

        private static bool AllowedVisualCall(MethodInfo m)
        {
            Type t = m.DeclaringType;
            return t == typeof(FMODUnity.EventReference) && m.Name == "get_IsNull" ||
                t == typeof(FMODUnity.RuntimeManager) && m.Name == "PlayOneShot" ||
                t == typeof(SpriteFx) && (m.Name == "get_Pool" || m.Name == "SetBodyYPos" || m.Name == "SetRotation" || m.Name == "SetScale" || m.Name == "SetTransparentOnMultiplayer") ||
                t == typeof(ObjectPoolingFactory<SpriteFx>) && m.Name == "Spawn" ||
                t == typeof(UnityEngine.Random) && (m.Name == "Range" || m.Name == "get_insideUnitCircle") ||
                t == typeof(Vector2) && (m.Name == "op_Multiply" || m.Name == "op_Implicit") ||
                t == typeof(Vector3) && m.Name == "op_Addition" ||
                t == typeof(Mathf) && m.Name == "Sign" ||
                t == typeof(Transform) && (m.Name == "set_localScale" || m.Name == "get_localPosition") ||
                t == typeof(Quaternion) && m.Name == "get_identity" ||
                t == typeof(UnityEngine.Object) && (m.Name == "op_Implicit" || m.Name == "Instantiate") ||
                t == typeof(Component) && (m.Name == "get_transform" || m.Name == "get_gameObject") ||
                t == typeof(GameObject) && (m.Name == "SetActive" || m.Name == "GetComponent") ||
                t == typeof(Unit_LibraryLivingStatue_Fragment) && m.Name == "SetYPos" ||
                t == typeof(GameCamera) && m.Name == "get_Observer" ||
                m.ReturnType == typeof(GameCamera) && m.Name == "get_Instance" ||
                t == typeof(Mirror.NetworkBehaviour) && m.Name == "get_netId" ||
                t == typeof(List<GameObject>) && (m.Name == "get_Count" || m.Name == "get_Item") ||
                t == typeof(List<Unit_LibraryLivingStatue_Fragment>) && (m.Name == "get_Count" || m.Name == "GetEnumerator") ||
                t == typeof(List<Unit_LibraryLivingStatue_Fragment>.Enumerator) && (m.Name == "get_Current" || m.Name == "MoveNext") ||
                t == typeof(IDisposable) && m.Name == "Dispose";
        }
    }
}
