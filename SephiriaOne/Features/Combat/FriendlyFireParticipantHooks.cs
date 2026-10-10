using System;
using System.Reflection;
using HarmonyLib;
using Mirror;

namespace SephiriaOne
{
    internal static class FriendlyFireParticipantHooks
    {
        internal static void Install(Harmony harmony)
        {
            Prefix(harmony, typeof(PlayerSpawner), "OnDestroy", Type.EmptyTypes, nameof(BeforeDestroy));
            Prefix(harmony, typeof(PlayerSpawner), "OnStartServer", Type.EmptyTypes, nameof(BeforeSpawn));
            var initialize = Require(typeof(PlayerSpawner), "Initialize", typeof(bool), false,
                new[] { typeof(int), typeof(string), typeof(string), typeof(int) });
            harmony.Patch(initialize, prefix: Hook(nameof(BeforeInitialize)), finalizer: Hook(nameof(AfterInitialize), Priority.Last));
            Prefix(harmony, typeof(HorayNetworkManager), "OnServerDisconnect", new[] { typeof(NetworkConnectionToClient) }, nameof(BeforeDisconnect));
            Prefix(harmony, typeof(HorayNetworkManager), "OnStopServer", Type.EmptyTypes, nameof(BeforeStop));
            Prefix(harmony, typeof(HorayNetworkManager), "OnStopClient", Type.EmptyTypes, nameof(BeforeStop));
            Prefix(harmony, typeof(HorayNetworkManager), "OnStartServer", Type.EmptyTypes, nameof(BeforeStart));
            harmony.Patch(Require(typeof(NetworkServer), "RemoveConnection", typeof(bool), true, new[] { typeof(int) }),
                prefix: Hook(nameof(BeforeRemoveConnection)));
            harmony.Patch(Require(typeof(NetworkServer), "AddConnection", typeof(bool), true, new[] { typeof(NetworkConnectionToClient) }),
                postfix: Hook(nameof(AfterAddConnection)));
        }

        private static void Prefix(Harmony harmony, Type type, string name, Type[] parameters, string hook) =>
            harmony.Patch(Require(type, name, typeof(void), false, parameters), prefix: Hook(hook));

        private static MethodInfo Require(Type type, string name, Type result, bool isStatic, Type[] parameters)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters);
            if (method == null || method.ReturnType != result || method.IsStatic != isStatic)
                throw new MissingMethodException("Native participant lifecycle changed: " + type.Name + "." + name);
            return method;
        }
        private static HarmonyMethod Hook(string name, int priority = Priority.First) =>
            new HarmonyMethod(typeof(FriendlyFireParticipantHooks), name) { priority = priority };
        private static void BeforeDestroy(PlayerSpawner __instance) => FriendlyFireParticipants.Retire(__instance);
        private static void BeforeSpawn(PlayerSpawner __instance) => FriendlyFireParticipants.Spawned(__instance);
        private static void BeforeInitialize(PlayerSpawner __instance, out FriendlyFireParticipants.Initialization __state) =>
            __state = FriendlyFireParticipants.Initializing(__instance);
        private static Exception AfterInitialize(Exception __exception, bool __result, FriendlyFireParticipants.Initialization __state)
        {
            FriendlyFireParticipants.Initialized(__state, __exception == null && __result);
            return __exception;
        }
        private static void BeforeDisconnect(NetworkConnectionToClient conn) => FriendlyFireParticipants.Retire(conn);
        private static void BeforeRemoveConnection(int connectionId)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out NetworkConnectionToClient connection))
                FriendlyFireParticipants.Retire(connection);
        }
        private static void AfterAddConnection(NetworkConnectionToClient conn, bool __result)
        { if (__result) FriendlyFireParticipants.Connected(conn); }
        private static void BeforeStop() => FriendlyFireParticipants.ServerStopping();
        private static void BeforeStart() => FriendlyFireParticipants.ServerStarted();
    }
}
