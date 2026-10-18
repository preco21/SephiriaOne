using System;
using System.Collections.Generic;
using System.Linq;

internal static class LifecycleProbe
{
    internal static readonly List<string> Events = new List<string>();
    internal static Action<string> Initializing;
    internal static Action<string> ShuttingDown;
    internal static Action<string> CreatingComponent;
    internal static void Initialize(string name) { Events.Add("init:" + name); Initializing?.Invoke(name); }
    internal static void Shutdown(string name) { Events.Add("stop:" + name); ShuttingDown?.Invoke(name); }
    internal static int Count(string value) => Events.Count(e => e == value);
    internal static void Reset() { Events.Clear(); Initializing = null; ShuttingDown = null; CreatingComponent = null; }
}

public class HorayModBase
{
    public struct Metadata { public string modVersion; }
    public Metadata metadata;
    public void LoadMod(Metadata value) { metadata = value; OnModLoaded(); }
    public void UnloadMod() => OnModUnloaded();
    protected virtual void OnModLoaded() { }
    protected virtual void OnModUnloaded() { }
}

internal static class HorayModAPI
{
    internal static event Action OnAllDatabasesReady;
    internal static int SubscriberCount => OnAllDatabasesReady?.GetInvocationList().Length ?? 0;
    internal static void Ready() => OnAllDatabasesReady?.Invoke();
}

namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed &&
            (!(value is Component component) || !component.gameObject.Destroyed);
        public static void DontDestroyOnLoad(Object value) => LifecycleProbe.Events.Add("persistent");
        public static void Destroy(Object value) { value.Destroyed = true; LifecycleProbe.Events.Add("destroy"); }
    }
    public class GameObject : Object
    {
        internal static readonly List<GameObject> Created = new List<GameObject>();
        internal readonly List<Component> Components = new List<Component>();
        public GameObject(string name) { Created.Add(this); LifecycleProbe.Events.Add("object:" + name); }
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            Components.Add(component);
            LifecycleProbe.Events.Add("component:" + typeof(T).Name);
            LifecycleProbe.CreatingComponent?.Invoke(typeof(T).Name);
            return component;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        private bool active = true;
        public bool enabled
        {
            get => active;
            set { active = value; LifecycleProbe.Events.Add("enabled:" + GetType().Name + ":" + value); }
        }
    }
    internal static class Application { internal static string persistentDataPath => "fixture-data"; }
    internal static class Debug { internal static void Log(object value) { } internal static void LogWarning(object value) { } }
}

namespace SephiriaOne
{
    internal sealed class MultiplayerNameController : UnityEngine.Component { }
    internal sealed class ModChatCommands : UnityEngine.Component { }
    internal sealed class SessionSettingsController : UnityEngine.Component { }
    internal sealed class SettingsPanelController : UnityEngine.Component { }
    internal sealed class UpdateController : UnityEngine.Component { }
    internal sealed class HotkeyController : UnityEngine.Component { }
    internal static class L
    {
        internal static void Initialize(string path, Action<string> warning) => LifecycleProbe.Initialize("Localization");
        internal static void Shutdown() => LifecycleProbe.Shutdown("Localization");
    }
    internal static class ChoiceFeature { internal static void Initialize() => LifecycleProbe.Initialize("Choice"); internal static void Shutdown() => LifecycleProbe.Shutdown("Choice"); }
    internal static class SessionBoundaryFeature { internal static void Initialize() => LifecycleProbe.Initialize("SessionBoundary"); internal static void Shutdown() => LifecycleProbe.Shutdown("SessionBoundary"); }
    internal static class ResourceFeature { internal static void Initialize() => LifecycleProbe.Initialize("Resource"); internal static void Shutdown() => LifecycleProbe.Shutdown("Resource"); }
    internal static class RabbitPotionFeature { internal static void Initialize() => LifecycleProbe.Initialize("RabbitPotion"); internal static void Shutdown() => LifecycleProbe.Shutdown("RabbitPotion"); }
    internal static class RabbitLevelUpFeature { internal static void Initialize() => LifecycleProbe.Initialize("RabbitLevelUp"); internal static void Shutdown() => LifecycleProbe.Shutdown("RabbitLevelUp"); internal static void OnDatabasesReady() => LifecycleProbe.Events.Add("ready:RabbitLevelUp"); }
    internal static class RabbitDescriptionFeature { internal static void Initialize() => LifecycleProbe.Initialize("RabbitDescription"); internal static void Shutdown() => LifecycleProbe.Shutdown("RabbitDescription"); }
    internal static class MerchantFeature { internal static void Initialize() => LifecycleProbe.Initialize("Merchant"); internal static void Shutdown() => LifecycleProbe.Shutdown("Merchant"); }
    internal static class ItemRestrictionFeature { internal static void Initialize() => LifecycleProbe.Initialize("ItemRestriction"); internal static void Shutdown() => LifecycleProbe.Shutdown("ItemRestriction"); }
    internal static class FriendlyFireFeature { internal static void Initialize() => LifecycleProbe.Initialize("FriendlyFire"); internal static void Shutdown() => LifecycleProbe.Shutdown("FriendlyFire"); }
    internal static class LobbyRegenFeature { internal static void Initialize() => LifecycleProbe.Initialize("LobbyRegen"); internal static void Shutdown() => LifecycleProbe.Shutdown("LobbyRegen"); }
    internal static class ReviveAllFeature { internal static void Initialize() => LifecycleProbe.Initialize("ReviveAll"); internal static void Shutdown() => LifecycleProbe.Shutdown("ReviveAll"); }
    internal static class DeathmatchFeature { internal static void Initialize() => LifecycleProbe.Initialize("Deathmatch"); internal static void Shutdown() => LifecycleProbe.Shutdown("Deathmatch"); }
    internal static class JarSpawnFeature { internal static void Initialize() => LifecycleProbe.Initialize("JarSpawn"); internal static void Shutdown() => LifecycleProbe.Shutdown("JarSpawn"); }
    internal static class EventSpawnFeature { internal static void Initialize() => LifecycleProbe.Initialize("EventSpawn"); internal static void Shutdown() => LifecycleProbe.Shutdown("EventSpawn"); }
    internal static class BatCostumeFeature { internal static void Initialize() => LifecycleProbe.Initialize("BatCostume"); internal static void Shutdown() => LifecycleProbe.Shutdown("BatCostume"); }
    internal static class CollinFeature { internal static void Initialize() => LifecycleProbe.Initialize("Collin"); internal static void Shutdown() => LifecycleProbe.Shutdown("Collin"); internal static void OnDatabasesReady() => LifecycleProbe.Events.Add("ready:Collin"); }
    internal static class DisconnectDiagnosticsFeature { internal static void Initialize() => LifecycleProbe.Initialize("DisconnectDiagnostics"); internal static void Shutdown() => LifecycleProbe.Shutdown("DisconnectDiagnostics"); }
    internal static class RabbitStartingArtifactFeature { internal static void Apply() => LifecycleProbe.Events.Add("ready:RabbitStartingArtifact"); internal static void Shutdown() => LifecycleProbe.Shutdown("RabbitStartingArtifact"); }
}
