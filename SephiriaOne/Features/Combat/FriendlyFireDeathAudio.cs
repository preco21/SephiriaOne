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
    // Stock clients have no generic sound RPC. This prefab's base destroy
    // presentation is sound-only: no projectile is spawned/destroyed or damaged.
    internal static class FriendlyFireDeathAudio
    {
        private const string SoundPrefab = "FlameBall";
        private const string Rpc = "System.Void DungeonManager::RpcBulletDestroyed(System.UInt32,System.Boolean,System.String,UnityEngine.Vector3,System.Single,System.Single,System.Single)";
        private const int Hash = -1283831235;
        private static bool initialized, warned;
        private static MethodInfo targetSend;
        internal static void Reset() { initialized = warned = false; targetSend = null; }

        internal static bool SoundOnly(BulletDestroyModule module)
        {
            if (!module || module.GetType() != typeof(BulletDestroyModule) || module.destroySound.IsNull ||
                module.destroyFxPrefab || module.destroyFxPrefabList.Count != 0 || module.breakFxFragments.Count != 0) return false;
            var guid = module.destroySound.Guid;
            // Installed stock event:/Scene/hitMagicFire. A changed asset cannot
            // silently substitute a different or looping sound on unmodded peers.
            return guid.Data1 == -33958705 && guid.Data2 == 1201911055 && guid.Data3 == 338028947 && guid.Data4 == 494537983;
        }

        internal static void Show(PlayerAvatar victim)
        {
            try
            {
                var dungeon = DungeonManager.Instance;
                if (!NetworkServer.active || !dungeon || !dungeon.isServer || dungeon.netId == 0 ||
                    !victim || !victim.isServer || !victim.IsDead || !victim.spawner ||
                    !ReferenceEquals(victim.spawner.PlayerAvatar, victim) || !victim.netIdentity || !dungeon.netIdentity) return;
                Vector3 position = victim.transform.position;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) return;
                if (!initialized)
                {
                    initialized = true;
                    // The existing adapter audits this same cosmetic sender,
                    // receiver, serializer and base module (including its audio).
                    if (!FriendlyFireDeathVisuals.Validate() || !ValidateLocalDeathSound()) throw new InvalidOperationException("Native impact audio transport changed.");
                    targetSend = AccessTools.DeclaredMethod(typeof(NetworkBehaviour), "SendTargetRPCInternal",
                        new[] { typeof(NetworkConnection), typeof(string), typeof(int), typeof(NetworkWriter), typeof(int) });
                    if (targetSend == null) throw new InvalidOperationException("Native targeted cosmetic sender unavailable.");
                }
                if (targetSend == null) return;
                var bullet = Bullet.Pool?.GetPrefab(SoundPrefab);
                if (!bullet || !bullet.DestroyModule) return; // Temporary pool teardown.
                if (!SoundOnly(bullet.DestroyModule)) throw new InvalidOperationException("Native sound-only impact prefab changed.");
                var writer = NetworkWriterPool.Get();
                try
                {
                    writer.WriteVarUInt(victim.netId); writer.WriteBool(false); writer.WriteString(SoundPrefab);
                    writer.WriteVector3(position); writer.WriteFloat(0); writer.WriteFloat(0); writer.WriteFloat(1);
                    // Dictionary keys and current map identity exclude stale or
                    // duplicate observer entries. Never also play audio locally.
                    foreach (var entry in victim.netIdentity.observers.ToArray())
                    {
                        var connection = entry.Value;
                        if (connection == null || connection.connectionId != entry.Key || !connection.isReady ||
                            ReferenceEquals(connection, victim.connectionToClient) ||
                            !NetworkServer.connections.TryGetValue(entry.Key, out var live) || !ReferenceEquals(connection, live) ||
                            !dungeon.netIdentity.observers.TryGetValue(entry.Key, out var observer) || !ReferenceEquals(observer, connection) ||
                            !connection.identity || !connection.identity.TryGetComponent<PlayerSpawner>(out var spawner) ||
                            !ReferenceEquals(spawner.connectionToClient, connection) || !HostStateAdapter.IsReady(spawner) ||
                            !spawner.PlayerAvatar || !ReferenceEquals(spawner.netIdentity, connection.identity) ||
                            !ReferenceEquals(spawner.PlayerAvatar.spawner, spawner) || !ReferenceEquals(spawner.PlayerAvatar.connectionToClient, connection) ||
                            !ReferenceEquals(spawner.PlayerAvatar.netIdentity, connection.identity) ||
                            spawner.PlayerAvatar.currentFloorGuid != victim.currentFloorGuid) continue;
                        try { targetSend.Invoke(dungeon, new object[] { connection, Rpc, Hash, writer, 0 }); }
                        catch (Exception error) { Warn(error); } // No retries after an ambiguous send.
                    }
                }
                finally { NetworkWriterPool.Return(writer); }
            }
            catch (Exception error) { targetSend = null; Warn(error); }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool ValidateLocalDeathSound()
        {
            var method = AccessTools.DeclaredMethod(typeof(PlayerSpawner), "HandleDieClientside", Type.EmptyTypes);
            return method != null && ValidateLocalDeathCode(PatchProcessor.GetOriginalInstructions(method));
        }
        internal static bool ValidateLocalDeathCode(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Where(i => i.opcode != OpCodes.Nop).ToList();
            int gate = code.FindIndex(i => i.operand is MethodInfo m && m.DeclaringType == typeof(NetworkBehaviour) && m.Name == "get_isLocalPlayer");
            if (gate < 0 || gate + 1 >= code.Count || (code[gate + 1].opcode != OpCodes.Brfalse && code[gate + 1].opcode != OpCodes.Brfalse_S) ||
                !(code[gate + 1].operand is Label target)) return false;
            int end = code.FindIndex(i => i.labels.Contains(target));
            var audio = code.Select((i, index) => (i, index)).Where(x => x.i.operand is MethodInfo m &&
                (m.DeclaringType?.FullName == "FMODUnity.RuntimeManager" && m.Name == "CreateInstance" ||
                 m.DeclaringType?.FullName == "FMOD.Studio.EventInstance" && (m.Name == "start" || m.Name == "release"))).ToList();
            return audio.Count == 3 && audio.All(x => x.index > gate && x.index < end) &&
                code.Any(i => i.operand is FieldInfo f && f.DeclaringType == typeof(PlayerSpawner) && f.Name == "dieSoundEvent");
        }
        private static void Warn(Exception error)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[SephiriaOne] Multiplayer death audio unavailable; combat and visuals remain active: " + error);
        }
    }
}
