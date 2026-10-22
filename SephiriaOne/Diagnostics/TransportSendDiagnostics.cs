using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

namespace SephiriaOne
{
    // Observe native failures only. Never retry, alter payloads, disconnect, or
    // reconcile gameplay state from inside the transport's send stack.
    internal static class TransportSendDiagnostics
    {
        private const string Owner = "preco21.SephiriaOne.TransportSendDiagnostics";
        private const float ReportInterval = 5f;
        private sealed class Failures
        {
            internal long Count, Bytes, Reliable, Unreliable;
            internal float First, Last, NextReport;
        }
        private static ConditionalWeakTable<NetworkConnectionToClient, Failures> failures =
            new ConditionalWeakTable<NetworkConnectionToClient, Failures>();
        private static readonly ConditionalWeakTable<NetworkConnectionToClient, Failures>.CreateValueCallback create = _ => new Failures();
        private static Harmony harmony;
        private static FieldInfo connectionMap;

        internal static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                var target = AccessTools.DeclaredMethod(typeof(NextServer).BaseType, "SendSocket",
                    new[] { typeof(HSteamNetConnection), typeof(byte[]), typeof(int) });
                connectionMap = AccessTools.DeclaredField(typeof(NextServer), "connToMirrorID");
                if (target == null || target.IsStatic || target.ReturnType != typeof(EResult) ||
                    connectionMap == null || connectionMap.FieldType != typeof(BidirectionalDictionary<HSteamNetConnection, int>))
                    throw new MissingMethodException("Steam send diagnostic boundary changed.");
                instance.Patch(target, postfix: new HarmonyMethod(typeof(TransportSendDiagnostics), nameof(AfterSend)));
                harmony = instance;
            }
            catch (Exception error)
            {
                instance.UnpatchAll(Owner);
                DisconnectDiagnostics.Write("send diagnostics unavailable: " + DisconnectDiagnostics.OneLine(error.Message));
            }
        }

        internal static void Uninstall()
        {
            harmony?.UnpatchAll(Owner); harmony = null; connectionMap = null;
            failures = new ConditionalWeakTable<NetworkConnectionToClient, Failures>();
        }

        private static void AfterSend(object __instance, HSteamNetConnection __0, byte[] __1, int __2, EResult __result)
        {
            // The normal hot path has no clock reads, reflection, allocation or logging.
            if (__result == EResult.k_EResultOK) return;
            try
            {
                if (!NetworkServer.active || !(__instance is NextServer server) ||
                    !(connectionMap.GetValue(server) is BidirectionalDictionary<HSteamNetConnection, int> map) ||
                    !map.TryGetValue(__0, out int id) ||
                    !NetworkServer.connections.TryGetValue(id, out var connection) || connection == null) return;
                float now = Time.realtimeSinceStartup;
                Failures state = failures.GetValue(connection, create);
                if (state.Count == 0) state.First = now;
                state.Count++; state.Bytes += __1 == null ? 0 : __1.Length;
                if (__2 == 1) state.Unreliable++; else state.Reliable++;
                state.Last = now;
                if (state.Count != 1 && now < state.NextReport) return;
                state.NextReport = now + ReportInterval;
                DisconnectDiagnostics.Write("send-failure conn=" + id + "; result=" + __result + "; " + Describe(state) +
                    "; sendBufferBytes=" + (__1 == null ? 0 : __1.Length) + "; channel=" + __2 +
                    "; networkSpawned=" + NetworkServer.spawned.Count + "; " + ProjectileSnapshot(connection));
            }
            catch { } // Diagnostics cannot change native transport behavior.
        }

        internal static void BeforeRemoval(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            try
            {
                if (failures.TryGetValue(connection, out var state))
                    DisconnectDiagnostics.Write("send-summary conn=" + connection.connectionId + "; " + Describe(state));
            }
            catch { }
            finally { failures.Remove(connection); }
        }

        internal static void BeforeClose(int id)
        {
            // Mirror retires ledgers in RemoveConnection BEFORE invoking the game
            // manager's OnServerDisconnect. Capture live counts at transport entry.
            try
            {
                if (NetworkServer.connections.TryGetValue(id, out var connection) && connection != null)
                    DisconnectDiagnostics.Write("pre-close conn=" + id + "; networkSpawned=" + NetworkServer.spawned.Count +
                        "; " + ProjectileSnapshot(connection));
            }
            catch { }
        }

        private static string ProjectileSnapshot(NetworkConnectionToClient connection)
        {
            try { return ProjectileLimitRuntime.DescribeDiagnostics(connection); }
            catch { return "projectile-limits snapshot unavailable"; }
        }

        // SendSocket appends its channel byte to the argument before the postfix.
        // These are attempted native buffers, not delivered bytes or wire overhead.
        private static string Describe(Failures state) => "failures=" + state.Count + "; failedSendBytes=" + state.Bytes +
            "; reliable=" + state.Reliable + "; unreliable=" + state.Unreliable +
            "; failureSpanSeconds=" + (state.Last - state.First).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
