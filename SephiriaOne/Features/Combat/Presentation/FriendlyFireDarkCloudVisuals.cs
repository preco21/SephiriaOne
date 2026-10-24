using System;
using System.Buffers;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // The native sender addresses only the owner. Forward that same cosmetic
    // message, never a second strike, to other existing observers.
    internal static class FriendlyFireDarkCloudVisuals
    {
        internal struct StrikeScope
        {
            internal ComboEffect_DarkCloud Cloud;
            internal NetworkIdentity Identity;
            internal NetworkConnectionToClient Owner;
            internal FriendlyFireParticipants.Receipt Source, Victim;
            internal uint NetId;
            internal long Generation;
            internal string Floor;
            internal bool Sent;
        }
        internal struct ScopeState
        {
            internal bool Entered;
            internal StrikeScope Previous;
        }
        [ThreadStatic] private static StrikeScope current;
        internal static Action<ComboEffect_DarkCloud, NetworkConnectionToClient, Vector3, bool, bool> send;
        private static bool warned;
        private static long generation;

        internal static void BeforeStrike(ComboEffect_DarkCloud __instance, UnitAvatar target, out ScopeState __state)
        {
            __state = new ScopeState { Entered = true, Previous = current };
            current = default; // Unrelated nested strikes must not borrow admission.
            try
            {
                if (send == null || !NetworkServer.active || !__instance || !__instance.isServer || !__instance.isPlayerCombo ||
                    !(__instance.Networkavatar is PlayerAvatar source) || !(target is PlayerAvatar victim) ||
                    !source || !victim || ReferenceEquals(source, victim) || victim.IsDead ||
                    string.IsNullOrEmpty(source.currentFloorGuid) || source.currentFloorGuid != victim.currentFloorGuid ||
                    !FriendlyFireRuntime.CanShowDarkCloudStrike(source, victim)) return;
                var attacker = FriendlyFireParticipants.Capture(source);
                var defender = FriendlyFireParticipants.Capture(victim);
                if (attacker == null || defender == null) return;
                var scope = new StrikeScope { Cloud = __instance, Identity = __instance.netIdentity, NetId = __instance.netId,
                    Owner = __instance.connectionToClient, Source = attacker, Victim = defender, Floor = source.currentFloorGuid,
                    Generation = generation };
                if (Current(scope)) current = scope;
            }
            catch (Exception error) { Warn(error); }
        }

        internal static Exception AfterStrike(Exception __exception, ScopeState __state)
        {
            // Harmony can skip this prefix after an earlier limiter veto. Its
            // finalizer must then leave the surrounding strike's scope alone.
            if (__state.Entered) current = __state.Previous;
            return __exception;
        }

        internal static void AfterNativeSend(ComboEffect_DarkCloud __instance, NetworkConnectionToClient __0,
            Vector3 __1, bool __2, bool __3)
        {
            var scope = current;
            if (scope.Sent || !ReferenceEquals(scope.Cloud, __instance) || !ReferenceEquals(scope.Owner, __0)) return;
            current.Sent = true; // Set before any reentrant callback or ambiguous send.
            try
            {
                if (!Finite(__1.x) || !Finite(__1.y) || !Finite(__1.z) || !Current(scope)) return;
                var observers = scope.Identity.observers;
                int count = observers.Count;
                if (count == 0) return;
                var snapshot = ArrayPool<NetworkConnectionToClient>.Shared.Rent(count);
                try
                {
                    int copied = 0;
                    foreach (var entry in observers)
                        if (entry.Value != null && entry.Key == entry.Value.connectionId) snapshot[copied++] = entry.Value;
                    for (int i = 0; i < copied; i++)
                    {
                        if (!Current(scope)) break;
                        var connection = snapshot[i];
                        if (ReferenceEquals(connection, scope.Owner) || !connection.isReady ||
                            !NetworkServer.connections.TryGetValue(connection.connectionId, out var live) || !ReferenceEquals(live, connection) ||
                            !observers.TryGetValue(connection.connectionId, out var observed) || !ReferenceEquals(observed, connection) ||
                            !connection.identity) continue;
                        var spawner = connection.identity.GetComponent<PlayerSpawner>();
                        if (!spawner || !spawner.PlayerAvatar || !ReferenceEquals(spawner.connectionToClient, connection) ||
                            spawner.PlayerAvatar.currentFloorGuid != scope.Floor) continue;
                        var recipient = FriendlyFireParticipants.Capture(spawner.PlayerAvatar);
                        if (recipient == null || !ReferenceEquals(recipient.Connection, connection)) continue;
                        try { send(scope.Cloud, connection, __1, __2, __3); }
                        catch (Exception error) { Warn(error); } // Never retry a possibly delivered cosmetic message.
                    }
                }
                finally { ArrayPool<NetworkConnectionToClient>.Shared.Return(snapshot, clearArray: true); }
            }
            catch (Exception error) { Warn(error); }
        }

        private static bool Current(StrikeScope scope) => scope.Generation == generation && send != null && NetworkServer.active && scope.Cloud &&
            scope.Cloud.isServer && scope.Cloud.isPlayerCombo && scope.Identity && scope.NetId != 0 &&
            ReferenceEquals(scope.Cloud.netIdentity, scope.Identity) && scope.Cloud.netId == scope.NetId &&
            NetworkServer.spawned.TryGetValue(scope.NetId, out var spawned) && ReferenceEquals(spawned, scope.Identity) &&
            FriendlyFireParticipants.IsCurrent(scope.Source) && FriendlyFireParticipants.IsCurrent(scope.Victim) &&
            ReferenceEquals(scope.Cloud.Networkavatar, scope.Source.Player) &&
            ReferenceEquals(scope.Cloud.connectionToClient, scope.Owner) && ReferenceEquals(scope.Source.Connection, scope.Owner) &&
            scope.Source.Player.currentFloorGuid == scope.Floor && scope.Victim.Player.currentFloorGuid == scope.Floor;

        internal static void Reset() { generation++; current = default; send = null; warned = false; }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static void Warn(Exception error)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[SephiriaOne] Dark Cloud observer FX unavailable; native owner FX and combat remain active: " + error);
        }
    }
}
