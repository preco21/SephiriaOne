// These stubs reproduce only the inspected native lifecycle ordering. They do not
// establish Unity component semantics, Harmony installation, or guest rendering.
using System.Reflection;
using SephiriaOne;
using UnityEngine;

namespace UnityEngine
{
    public static class Time { public static double timeAsDouble, unscaledTimeAsDouble; }
    public class Object
    {
        internal bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component { }
    public class GameObject : Object
    {
        private readonly List<Component> components = new();
        public string name;
        public GameObject(string name = "") { this.name = name; }
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            components.Add(component);
            return component;
        }
        public T GetComponent<T>() where T : Component => components.OfType<T>().FirstOrDefault();
        public void DestroyNow()
        {
            foreach (var component in components.ToArray())
                component.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(component, null);
            foreach (var component in components) component.Destroyed = true;
            Destroyed = true;
            var identity = GetComponent<Mirror.NetworkIdentity>();
            if (identity != null) Mirror.NetworkServer.spawned.Remove(identity.netId);
        }
    }
    public struct Vector2 { }
    public struct Vector3 { }
}

namespace Mirror
{
    public sealed class NetworkIdentity : Component { public uint netId; }
    public sealed class NetworkConnectionToClient
    {
        public int connectionId;
        public NetworkIdentity identity;
    }
    public static class NetworkServer
    {
        public static bool active;
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new();
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new();
        private static uint nextId;
        public static NetworkIdentity Register(GameObject gameObject)
        {
            var identity = gameObject.GetComponent<NetworkIdentity>() ?? gameObject.AddComponent<NetworkIdentity>();
            identity.netId = ++nextId;
            spawned.Add(identity.netId, identity);
            return identity;
        }
    }
}

public enum EDamageFromType { None, DirectAttack }
public enum EDamageElementalType { Physical, Fire }
public class CombatBehaviour : MonoBehaviour { }
public class DamageInstance { }
public class UnitAvatar : CombatBehaviour { public UnitAvatar NetworkLeader; }
public class PlayerAvatar : UnitAvatar
{
    public bool isServer = true;
    public PlayerSpawner spawner;
}
public class PlayerSpawner : MonoBehaviour
{
    public PlayerAvatar PlayerAvatar;
    public Mirror.NetworkConnectionToClient connectionToClient;
    public Mirror.NetworkIdentity netIdentity;
}
public class DungeonManager : MonoBehaviour { public static DungeonManager Instance; }
public class ProjectileBase : MonoBehaviour
{
    public string spawnedPrefabName;
    public Action SpawnCallback;
    public virtual void OnSpawn() => SpawnCallback?.Invoke();
}
public class Bullet : ProjectileBase
{
    public string damageId;
    public UnitAvatar NetworkOwner;
    public bool IsSpawned;
    public override void OnSpawn() { IsSpawned = true; base.OnSpawn(); }
}
public class SpecialProjectile_FuryMP : ProjectileBase { public UnitAvatar Networkowner; }
public class BulletDestroyModule : MonoBehaviour { public Bullet Bullet; }
public class NewWeaponFireData_SpecialProjectile { public GameObject projectilePrefab; }
public class ProjectileSpawner<T> where T : ProjectileBase
{
    public int NativeCalls;
    public Action<Bullet> OnSpawn;
    public Action<Bullet> OnInitialize;
    public Action BeforeInstantiation;
    public readonly List<Bullet> Created = new();
    public T Spawn(GameObject key, Vector3 position, bool transparent, EDamageFromType fromType, string damageId,
        float damage, int stagger, float force, UnitAvatar avatar, long targets, float height, Vector2 begin,
        Vector2 end, List<CombatBehaviour> shared, Action<CombatBehaviour, DamageInstance, ProjectileBase> attack,
        float range, EDamageElementalType element, bool showSwing)
    {
        NativeCalls++;
        if (!key) return null;
        BeforeInstantiation?.Invoke();
        var go = new GameObject(key.name + "(Clone)");
        var bullet = go.AddComponent<Bullet>();
        Created.Add(bullet);
        bullet.SpawnCallback = () => OnSpawn?.Invoke(bullet);
        // ProjectileSpawner<T>: Instantiate -> OnSpawn -> Initialize ->
        // NetworkServer.Spawn -> OnSpawnFinalized -> spawnedPrefabName.
        ProjectileLimitRuntime.BeforeSpawn(bullet);
        bullet.OnSpawn();
        bullet.damageId = damageId;
        bullet.NetworkOwner = avatar;
        OnInitialize?.Invoke(bullet);
        Mirror.NetworkServer.Register(go);
        bullet.spawnedPrefabName = key.name;
        return (T)(ProjectileBase)bullet;
    }
}

namespace SephiriaOne
{
    internal static class ProjectileLimitFeature { internal static bool Available = true; }
    internal static class SessionSettings
    {
        internal static ProjectileLimitSettings ProjectilesForUse;
        internal static long ResourceGeneration;
    }
}
