using System.Runtime.CompilerServices;
using Mirror;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public string name = "";
        public bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed &&
            (!(value is Component component) || component.gameObject == null || !component.gameObject.Destroyed);
        public static GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            FixtureWorld.BeforeInstantiate?.Invoke();
            var clone = prefab.Clone(); clone.transform.position = position; FixtureWorld.Created.Add(clone); return clone;
        }
        public static void Destroy(Object value)
        {
            if (value == null) return;
            value.Destroyed = true;
            if (value is GameObject gameObject) foreach (var component in gameObject.Components) component.Destroyed = true;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public readonly Transform transform = new();
        public GameObject(string label = "") { name = label; }
        public T AddComponent<T>() where T : Component, new()
        { var value = new T { gameObject = this }; Components.Add(value); return value; }
        public T GetComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault();
        public bool TryGetComponent<T>(out T value) where T : class { value = GetComponent<T>(); return value != null; }
        public GameObject Clone()
        {
            var clone = new GameObject(name + " clone");
            foreach (var component in Components)
            {
                if (component is Unit_BabaMerchantHard avatar)
                {
                    var copied = clone.AddComponent<Unit_BabaMerchantHard>(); copied.monsterType = avatar.monsterType;
                    copied.maxHp = avatar.maxHp; copied.isHPCursed = avatar.isHPCursed;
                }
                else if (component is Unit_Soldier soldier)
                { var copied = clone.AddComponent<Unit_Soldier>(); copied.maxHp = soldier.maxHp; copied.monsterType = soldier.monsterType; }
                else if (component is Unit_TurtlePotion turtle)
                { var copied = clone.AddComponent<Unit_TurtlePotion>(); copied.maxHp = turtle.maxHp; copied.monsterType = turtle.monsterType; }
                else if (component is UnitAvatar unit) clone.AddComponent<UnitAvatar>().monsterType = unit.monsterType;
                else if (component is UnitAI_NewBasic) clone.AddComponent<UnitAI_NewBasic>();
                else if (component is Safe) clone.AddComponent<Safe>();
                else if (component is NetworkIdentity) clone.AddComponent<NetworkIdentity>();
            }
            return clone;
        }
    }
    public class Transform { public Vector3 position; public Vector3 localScale; }
    public readonly record struct Vector3(float x, float y, float z = 0)
    {
        public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
        public static implicit operator Vector2(Vector3 value) => new(value.x, value.y);
    }
    public readonly record struct Vector2(float x, float y)
    { public static implicit operator Vector3(Vector2 value) => new(value.x, value.y); }
    public readonly struct Quaternion { public static Quaternion identity => default; }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static void Log(object value) { }
        public static void LogWarning(object value) => Warnings.Add(value.ToString());
    }
}

namespace Mirror
{
    public class NetworkBehaviour : Component { public bool isServer = true; public uint netId = 1; }
    public class NetworkIdentity : Component { public uint netId, assetId = 17; }
    public class NetworkConnectionToClient { }
    public static class NetworkServer
    {
        public static bool active = true;
        public static readonly Dictionary<int, NetworkConnectionToClient> connections = new();
        public static readonly List<GameObject> Spawned = new();
        public static readonly List<GameObject> Destroyed = new();
        public static int SpawnCalls, FailSpawnAt;
        public static GameObject FailDestroy;
        public static Action<GameObject> OnSpawn;
        public static void Spawn(GameObject value)
        {
            SpawnCalls++;
            if (FailSpawnAt == SpawnCalls) throw new InvalidOperationException("injected network spawn failure");
            Spawned.Add(value);
            if (value.GetComponent<Safe>() is Safe safe) Safe.All.Add(safe);
            OnSpawn?.Invoke(value);
        }
        public static void Destroy(GameObject value)
        {
            if (ReferenceEquals(FailDestroy, value)) throw new InvalidOperationException("injected network destroy failure");
            Destroyed.Add(value); Spawned.Remove(value); UnityEngine.Object.Destroy(value);
        }
    }
}

public enum EPersonality { Rational, Aggressive }
public enum ERelationBehaviour { Friendly, Neutral, Hostile }
public enum EFactionAlignment { Neutral }
public enum EProceduralMerchantType { None, Vendor }
public enum EMonsterType { Normal, Miniboss }
public enum ECustomStat { AllDamageBonus, DamageReduction }
public sealed class ItemMetadata { }
public sealed class CharacterBuff { }
public sealed class LocalizedString { public string key = "Merchant_Papa_Name"; }
public class SocialIDEntity : UnityEngine.Object
{
    public GameObject avatarPrefab;
    public LocalizedString aName = new();
    public EFactionAlignment alignment;
    public EProceduralMerchantType proceduralMerchantType = EProceduralMerchantType.Vendor;
    public int startingMoney = 15;
    public ItemMetadata[] startingItems = Array.Empty<ItemMetadata>();
}
public static class SocialIDDatabase
{
    public static SocialIDEntity Template;
    public static readonly Dictionary<string, SocialIDEntity> Variants = new();
    public static SocialIDEntity FindByName(string name) => name == "Merchant_Papa" ? Template : Variants.GetValueOrDefault(name);
}
public sealed class PropEntity { public GameObject propPrefab; }
public static class PropDatabase
{
    public static PropEntity Stock;
    public static PropEntity FindPropById(string name) => name == "Mat_TravelerMerchant" ? Stock : null;
}
public class SaveData
{
    public readonly Dictionary<string, bool> Flags = new();
    public readonly Dictionary<string, int> Ints = new();
    public bool ContainsKey(string key) => Ints.ContainsKey(key) || Flags.ContainsKey(key);
    public int GetInt(string key, int fallback = 0) => Ints.TryGetValue(key, out int value) ? value : fallback;
    public void SetInt(string key, int value) => Ints[key] = value;
    public bool GetBool(string key, bool fallback = false) => Flags.TryGetValue(key, out bool value) ? value : fallback;
    public void SetBool(string key, bool value) => Flags[key] = value;
}
public static class SaveManager { public static SaveData CurrentRun = new(); }
public class FloorData { public string guid, name = "Dungeon", stageName = "Dungeon"; public bool isHidden, pocketDimension; public int Progress, difficulty; }
public class FloorGenerator : NetworkBehaviour
{
    public static readonly List<FloorGenerator> FloorGenerators = new();
    public bool GenerateSuccess = true, isSafeFloor, isTrainingFloor, EligibleRoom = true;
    public string guid;
    public int seed = 123;
    public FloorData DataOnServer;
    public Vector2 SpawnPosition;
    public readonly List<GameObject> floorRelatedNetworkObjects = new();
}
public class DungeonManager : NetworkBehaviour
{
    public static DungeonManager Instance;
    public UnityEngine.Object Race = new();
    public int DestinySeed, raceId;
    public int[] Opportunities = new[] { 0 };
    public readonly Dictionary<int, int> MainStageNumbers = new();
    public readonly Dictionary<string, FloorData> generatedFloors = new();
    public CharacterBuff crimeDebuff = new();
    public int CrimeCalls;
    public void GetStageStatBonusAtPosition(Vector3 position, out int hp, out int attack, out int defense)
    { hp = 100; attack = 10; defense = 4; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void NPCDeadCheckServerside(UnitAI_NewBasic npc, DamageInstance damage)
    { CrimeCalls++; if (damage?.origin is UnitAvatar player) player.ApplyBuff(crimeDebuff); }
}
public class RuntimeFactionManager : NetworkBehaviour
{
    public static RuntimeFactionManager Instance;
    public readonly Dictionary<string, long> factionLayers = new() { ["Undead"] = 1, ["Pillagers"] = 2, ["Player"] = 4, ["Merchant"] = 8 };
    public readonly Dictionary<string, int> relationValues = new() { ["Merchant_Player"] = 0 };
    public readonly Dictionary<string, long> tempDynamicEnemies = new();
    public int GlobalWrites;
    public bool ThrowRelation;
    public readonly HashSet<string> NonHostile = new();
    public long FindFactionLayer(string faction) => factionLayers.GetValueOrDefault(faction);
    public ERelationBehaviour GetRelationBehaviour(string from, string to, EPersonality personality)
    {
        if (ThrowRelation) throw new InvalidOperationException("injected relation lookup failure");
        return from != to && to == "Player" && !NonHostile.Contains(from) && personality == EPersonality.Aggressive ? ERelationBehaviour.Hostile : ERelationBehaviour.Neutral;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void SetTempEnemyRelation(string from, string to)
    { GlobalWrites++; tempDynamicEnemies[from] = FindFactionLayer(to); }
}
public class UnitAvatar : NetworkBehaviour
{
    public string faction = "Merchant";
    public EPersonality attackableTargetSelector;
    public EMonsterType monsterType = EMonsterType.Miniboss;
    public bool IsInBattle, IsDead;
    public int RandomID, Buffs, DeathCalls, isHPCursed;
    public float maxHp = 2500, HpBonus, Healed;
    public float finalMaxHp => HpBonus;
    public float MaxHp => maxHp + finalMaxHp * maxHp / 100f;
    public int BaseHpWrites;
    public bool KeptHpRatio;
    public readonly Dictionary<ECustomStat, int> Stats = new();
    public void SetRandomID(int id) => RandomID = id;
    public void ChangeFaction(string value) => faction = value;
    public void ChangeAttackableTargetSelector(EPersonality value) => attackableTargetSelector = value;
    public void AddMaxHpPercent(float value) => HpBonus += value;
    public void AddMaxHp(float value, bool keepHpRatio = true)
    { maxHp += value; BaseHpWrites++; KeptHpRatio = keepHpRatio; }
    public void AddCustomStat(ECustomStat stat, int value) => Stats[stat] = Stats.GetValueOrDefault(stat) + value;
    public void HealPercent(float value) => Healed += value;
    public void ApplyBuff(CharacterBuff buff) => Buffs++;
    public void ForceDie() { DeathCalls++; IsDead = true; }
}
public class Unit_BabaMerchantHard : UnitAvatar { }
public class Unit_Soldier : UnitAvatar { }
public class Unit_TurtlePotion : UnitAvatar { }
public class DamageInstance { public UnityEngine.Object origin; }
public class Safe : NetworkBehaviour
{
    public static readonly List<Safe> All = new();
    public UnitAI_NewBasic NetworkconnectedMerchant;
    public int StockGenerations;
    public static Safe Find(Vector3 position) => All.FirstOrDefault(safe => safe && Vector3.Distance(safe.transform.position, position) <= 10);
}
public class UnitAI_NewBasic : NetworkBehaviour
{
    public UnitAvatar Avatar => GetComponent<UnitAvatar>();
    public UnitAvatar CurrentTarget;
    public Safe NetworkMySafe;
    public bool CanTalk = true;
    public string SocialName, Role;
    public int TargetChanges;
    public void SetSocialID(string socialID, string nameSource, EPersonality personality, EFactionAlignment alignment,
        string roleName, EProceduralMerchantType merchant, int startingMoney, ItemMetadata[] startingItems)
    {
        SocialName = socialID; Role = roleName;
        Avatar.ChangeAttackableTargetSelector(personality);
        NetworkMySafe = Safe.Find(transform.position);
        if (!NetworkMySafe)
        {
            var stock = UnityEngine.Object.Instantiate(PropDatabase.Stock.propPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(stock);
            NetworkMySafe = stock.GetComponent<Safe>();
        }
        NetworkMySafe.NetworkconnectedMerchant = this;
        NetworkMySafe.StockGenerations++;
        if (FixtureWorld.FailSocialAfterStock) throw new InvalidOperationException("injected social initialization failure");
        // The game can change targeting during native initialization; the addon must reapply its final personality.
        Avatar.ChangeAttackableTargetSelector(EPersonality.Rational);
    }
    public void SetTarget(UnitAvatar value) { CurrentTarget = value; TargetChanges++; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OnDamaged(DamageInstance damage)
    { RuntimeFactionManager.Instance.SetTempEnemyRelation(Avatar.faction, ((UnitAvatar)damage.origin).faction); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void OnDie(DamageInstance damage) => DungeonManager.Instance.NPCDeadCheckServerside(this, damage);
    public void RunDamage(DamageInstance damage) => OnDamaged(damage);
    public void RunDeath(DamageInstance damage) => OnDie(damage);
}
public static class KeywordDatabase { public static int GetConstValue(string name) => name.Contains("Hp") ? 40 : 10; }

namespace SephiriaOne
{
    internal sealed class MerchantRoute
    {
        private readonly DungeonManager dungeon;
        private readonly bool initialized;
        public MerchantRoute(DungeonManager dungeon, SaveData run) { this.dungeon = dungeon; initialized = dungeon.Race; }
        public IReadOnlyList<int> Opportunities => initialized ? dungeon.Opportunities : Array.Empty<int>();
        public int LatestPosition { get; private set; } = -1;
        public int FloorNumber(int position) => dungeon.MainStageNumbers.TryGetValue(position, out int stage) ? stage :
            position < 0 ? 0 : dungeon.Opportunities.Count(value => value <= position);
        public int Position(FloorData data)
        {
            if (!initialized || data.isHidden || data.pocketDimension) return -1;
            LatestPosition = Math.Max(LatestPosition, data.Progress); return data.Progress;
        }
        public void ObserveHistory() { }
    }
    internal static class SessionSettings
    {
        public static bool MerchantSpawnsForUse;
        public static bool Guarantee = true;
        public static int MerchantSpawnChanceForUse = 100;
        public static int FirstFloor = 1, MaxPerRun;
        public static readonly Dictionary<string, MerchantSettings> Variants = new();
        public static bool AnyMerchantSpawnsForUse => MerchantSpawnsForUse || Variants.Values.Any(value => value.Enabled);
        public static MerchantSettings GetMerchantSettingsForUse(string id) => id == MerchantCatalog.DefaultId ?
            new MerchantSettings(MerchantSpawnsForUse, MerchantSpawnChanceForUse, FirstFloor, MaxPerRun, Guarantee) :
            Variants.TryGetValue(id, out var settings) ? settings : MerchantSettings.Defaults(MerchantCatalog.Find(id));
    }
    internal static class MerchantFeature { public static bool Available = true; }
    internal static class MerchantRooms
    {
        public static bool ValidateContracts() => true;
        public static bool TryChoose(FloorGenerator floor, out Vector2 position)
        { position = floor.SpawnPosition; return floor.EligibleRoom; }
        public static bool TryChoose(FloorGenerator floor, int salt, out Vector2 position)
        { position = new Vector2(floor.SpawnPosition.x + (salt == 0 ? 0 : salt == 0x50415059 ? 16 : 32), floor.SpawnPosition.y); return floor.EligibleRoom; }
    }
}
internal static class FixtureWorld
{
    public static readonly List<GameObject> Created = new();
    public static bool FailSocialAfterStock;
    public static Action BeforeInstantiate;
}
