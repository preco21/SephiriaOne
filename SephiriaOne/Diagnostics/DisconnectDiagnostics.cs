using System;
using System.Text;
using HarmonyLib;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using UnityEngine;

namespace SephiriaOne
{
    // Event-only, local diagnostics. Never reconcile, recover, send or suppress a
    // native call here: the socket/player may already be halfway through teardown.
    internal static class DisconnectDiagnostics
    {
        private const string Owner = "preco21.SephiriaOne.DisconnectDiagnostics";
        private static Harmony harmony;

        public static void Install()
        {
            if (harmony != null) return;
            var instance = new Harmony(Owner);
            try
            {
                Patch(instance, typeof(NextServer), "InternalDisconnect", new[] { typeof(int), typeof(HSteamNetConnection) }, nameof(BeforeSocketClose));
                Patch(instance, typeof(NextServer), "Disconnect", new[] { typeof(int) }, nameof(BeforeServerClose));
                Patch(instance, typeof(HorayNetworkManager), "OnServerDisconnect", new[] { typeof(NetworkConnectionToClient) }, nameof(BeforePlayerRemoval));
                harmony = instance;
            }
            catch { instance.UnpatchAll(Owner); throw; }
        }

        private static void Patch(Harmony instance, Type type, string method, Type[] parameters, string prefix)
        {
            var target = AccessTools.DeclaredMethod(type, method, parameters);
            if (target == null || target.IsStatic || target.ReturnType != typeof(void))
                throw new MissingMethodException(type.Name + "." + method + " changed.");
            instance.Patch(target, prefix: new HarmonyMethod(typeof(DisconnectDiagnostics), prefix));
        }

        public static void Uninstall()
        {
            harmony?.UnpatchAll(Owner);
            harmony = null;
        }

        private static void BeforeSocketClose(int connId, HSteamNetConnection socket)
        {
            try
            {
                // Read before InternalDisconnect overwrites the native reason with
                // "Graceful disconnect", including send failures before a callback.
                string detail = SteamNetworkingSockets.GetConnectionInfo(socket, out var info)
                    ? "state=" + info.m_eState + "; endReason=" + info.m_eEndReason + "; detail=" + OneLine(info.m_szEndDebug)
                    : "Steam connection info unavailable";
                Write("transport-close conn=" + connId + "; " + detail);
            }
            catch (Exception error) { Write("transport-close conn=" + connId + "; diagnostic unavailable: " + OneLine(error.Message)); }
        }

        private static void BeforeServerClose(int connectionId) => Write("server-requested close conn=" + connectionId);

        private static void BeforePlayerRemoval(NetworkConnectionToClient conn)
        {
            try
            {
                uint id = conn != null && conn.identity ? conn.identity.netId : 0;
                Write("player-removal conn=" + (conn == null ? "unknown" : conn.connectionId.ToString()) +
                    "; netId=" + id + "; managedHeapBytes=" + GC.GetTotalMemory(false) +
                    " (host estimate, not total process/GPU memory)");
                foreach (string line in SessionSettings.DescribeDisconnect(id)) Write(line);
            }
            catch (Exception error) { Write("disconnect state unavailable: " + OneLine(error.Message)); }
        }

        private static void Write(string text)
        {
            // Even a failing log sink must not stop native connection cleanup.
            try { Debug.LogWarning("[SephiriaOne] [disconnect " + DateTime.UtcNow.ToString("O") + "] " + text); }
            catch { }
        }

        internal static string OneLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "none";
            int count = Math.Min(text.Length, 256);
            var result = new StringBuilder(count);
            for (int i = 0; i < count; i++) result.Append(char.IsControl(text[i]) ? ' ' : text[i]);
            return result.ToString();
        }
    }
}
