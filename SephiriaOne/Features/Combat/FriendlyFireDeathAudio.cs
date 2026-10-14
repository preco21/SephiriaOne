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
        private static int traceBudget = 6;
        internal static void Reset() { initialized = warned = false; targetSend = null; traceBudget = 6; }

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
                    !ReferenceEquals(victim.spawner.PlayerAvatar, victim) || !victim.netIdentity || !dungeon.netIdentity)
                { Trace("skipped: server, dungeon or dead-player identity unavailable"); return; }
                Vector3 position = victim.transform.position;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z))
                { Trace("skipped: non-finite death position"); return; }
                if (!initialized)
                {
                    initialized = true;
                    // The existing adapter audits this same cosmetic sender,
                    // receiver, serializer and base module (including its audio).
                    if (!FriendlyFireDeathVisuals.Validate() || !ValidateSoundPlayback() || !ValidateLocalDeathSound())
                        throw new InvalidOperationException("Native impact audio transport changed.");
                    targetSend = AccessTools.DeclaredMethod(typeof(NetworkBehaviour), "SendTargetRPCInternal",
                        new[] { typeof(NetworkConnection), typeof(string), typeof(int), typeof(NetworkWriter), typeof(int) });
                    if (targetSend == null) throw new InvalidOperationException("Native targeted cosmetic sender unavailable.");
                }
                if (targetSend == null) { Trace("skipped: native transport unavailable"); return; }
                var bullet = Bullet.Pool?.GetPrefab(SoundPrefab);
                if (!bullet || !bullet.DestroyModule)
                { Trace("skipped: native sound prefab/pool unavailable"); return; }
                if (!SoundOnly(bullet.DestroyModule)) throw new InvalidOperationException("Native sound-only impact prefab changed.");
                var writer = NetworkWriterPool.Get();
                try
                {
                    writer.WriteVarUInt(victim.netId); writer.WriteBool(false); writer.WriteString(SoundPrefab);
                    writer.WriteVector3(position); writer.WriteFloat(0); writer.WriteFloat(0); writer.WriteFloat(1);
                    // The RPC is addressed to the dungeon, not the victim.
                    // Its payload uses a position and cosmetic owner ID only;
                    // it never resolves the victim, inventory, race or stats.
                    int queued = 0, hostQueued = 0, stale = 0, playerMissing = 0, floorMismatch = 0;
                    int victimExcluded = 0, legacyInventoryGate = 0, legacyObserverGate = 0, diagnosticErrors = 0;
                    uint victimId = victim.netId;
                    int dungeonObservers = dungeon.netIdentity.observers.Count, victimObservers = victim.netIdentity.observers.Count;
                    foreach (var entry in dungeon.netIdentity.observers.ToArray())
                    {
                        if (!NetworkServer.active || !ReferenceEquals(dungeon, DungeonManager.Instance) ||
                            !dungeon || !dungeon.isServer || !victim || !victim.IsDead) break;
                        var connection = entry.Value;
                        if (connection == null || connection.connectionId != entry.Key || !connection.isReady ||
                            !NetworkServer.connections.TryGetValue(entry.Key, out var live) || !ReferenceEquals(connection, live) ||
                            !dungeon.netIdentity.observers.TryGetValue(entry.Key, out var observer) || !ReferenceEquals(observer, connection))
                        { stale++; continue; }
                        if (ReferenceEquals(connection, victim.connectionToClient)) { victimExcluded++; continue; }
                        if (!connection.identity || !connection.identity.TryGetComponent<PlayerSpawner>(out var spawner) ||
                            !spawner || !spawner.isServer || spawner.netId == 0 ||
                            !ReferenceEquals(spawner.connectionToClient, connection) ||
                            !spawner.PlayerAvatar || !spawner.PlayerAvatar.isServer || spawner.PlayerAvatar.netId == 0 ||
                            !ReferenceEquals(spawner.netIdentity, connection.identity) ||
                            !ReferenceEquals(spawner.PlayerAvatar.spawner, spawner) || !ReferenceEquals(spawner.PlayerAvatar.connectionToClient, connection) ||
                            !ReferenceEquals(spawner.PlayerAvatar.netIdentity, connection.identity))
                        { playerMissing++; continue; }
                        if (string.IsNullOrEmpty(victim.currentFloorGuid) || spawner.PlayerAvatar.currentFloorGuid != victim.currentFloorGuid)
                        { floorMismatch++; continue; }
                        // Diagnostic-only comparison with the old gates. Never
                        // require gameplay readiness for cosmetic playback.
                        if (traceBudget > 0)
                        {
                            try
                            {
                                if (!HostStateAdapter.IsReady(spawner)) legacyInventoryGate++;
                                if (!victim.netIdentity.observers.TryGetValue(entry.Key, out var previous) || !ReferenceEquals(previous, connection)) legacyObserverGate++;
                            }
                            catch { diagnosticErrors++; } // Diagnostics cannot gate or disable audio.
                        }
                        try
                        {
                            targetSend.Invoke(dungeon, new object[] { connection, Rpc, Hash, writer, 0 });
                            queued++;
                            if (connection is LocalConnectionToClient) hostQueued++;
                        }
                        catch (Exception error) { Warn(error); } // No retries after an ambiguous send.
                    }
                    if (traceBudget > 0)
                        Trace($"victim={victimId}; dungeonObservers={dungeonObservers}; victimObservers={victimObservers}; " +
                            $"queued={queued} (host={hostQueued}); excludedVictim={victimExcluded}; stale={stale}; playerUnavailable={playerMissing}; otherOrMissingFloor={floorMismatch}; " +
                            $"oldInventoryGate={legacyInventoryGate}; oldVictimObserverGate={legacyObserverGate}; diagnosticErrors={diagnosticErrors}. Queueing is not a client playback acknowledgment.");
                }
                finally { NetworkWriterPool.Return(writer); }
            }
            catch (Exception error) { targetSend = null; Warn(error); }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Trace(string message)
        {
            if (traceBudget <= 0) return;
            traceBudget--;
            try { Debug.Log("[SephiriaOne] Death audio: " + message); }
            catch { } // A log sink must not disable this optional presentation.
        }
        internal static bool ValidateSoundPlayback()
        {
            var method = AccessTools.DeclaredMethod(typeof(BulletDestroyModule), "CreateDestroyVisualOnClient");
            return method != null && ValidateSoundCode(PatchProcessor.GetOriginalInstructions(method));
        }
        internal static bool ValidateSoundCode(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.Where(i => i.opcode != OpCodes.Nop).ToList();
            // Native entry must play the non-null event at argument 3 (world
            // position) before any FX, owner, observer or projectile test.
            return code.Count >= 9 && code[0].opcode == OpCodes.Ldarg_0 &&
                code[1].opcode == OpCodes.Ldflda && IsSoundField(code[1].operand) &&
                code[2].opcode == OpCodes.Call && code[2].operand is MethodInfo empty &&
                empty.DeclaringType?.FullName == "FMODUnity.EventReference" && empty.Name == "get_IsNull" &&
                (code[3].opcode == OpCodes.Brtrue || code[3].opcode == OpCodes.Brtrue_S) &&
                code[3].operand is Label afterSound && code[8].labels.Contains(afterSound) &&
                code[4].opcode == OpCodes.Ldarg_0 && code[5].opcode == OpCodes.Ldfld && IsSoundField(code[5].operand) &&
                code[6].opcode == OpCodes.Ldarg_3 && code[7].opcode == OpCodes.Call &&
                code[7].operand is MethodInfo play && play.DeclaringType?.FullName == "FMODUnity.RuntimeManager" && play.Name == "PlayOneShot" &&
                play.ReturnType == typeof(void) && play.GetParameters().Select(p => p.ParameterType.FullName)
                    .SequenceEqual(new[] { "FMODUnity.EventReference", "UnityEngine.Vector3" });
        }
        private static bool IsSoundField(object operand) => operand is FieldInfo field &&
            field.DeclaringType == typeof(BulletDestroyModule) && field.Name == "destroySound";
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
