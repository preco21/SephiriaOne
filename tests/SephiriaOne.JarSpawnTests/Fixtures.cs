using System.Runtime.CompilerServices;
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object value) => value is not null; }
    public static class Debug { public static void LogWarning(object message) {} }
}
namespace Mirror
{
    public class NetworkBehaviour : UnityEngine.Object { [MethodImpl(MethodImplOptions.NoInlining)] public virtual void OnStartServer() {} }
    public static class NetworkServer
    {
        public static bool active = true;
        [MethodImpl(MethodImplOptions.NoInlining)] public static void Spawn(MysticPot pot) => pot.OnStartServer();
    }
}
namespace SephiriaOne
{
    internal static class SessionSettings { internal static JarSpawnSettings JarSpawnsForGeneration; }
}
public class DungeonManager : UnityEngine.Object
{
    public static DungeonManager Instance = new();
    public readonly Dictionary<string,int> dungeonEnvironment = new();
}
public class MysticPot : Mirror.NetworkBehaviour
{
    public int RandomID { [MethodImpl(MethodImplOptions.NoInlining)] get; set; }
    public int minChapterNum;
    public bool useRandomAppear = true;
    public float appearRate = .18f;
    public bool isGenerated = true;
    public bool NetworkisGenerated { [MethodImpl(MethodImplOptions.NoInlining)] get => isGenerated; [MethodImpl(MethodImplOptions.NoInlining)] set => isGenerated = value; }
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnStartServer()
    {
        base.OnStartServer();
        if (minChapterNum > 0 && (DungeonManager.Instance ? DungeonManager.Instance.dungeonEnvironment.GetValueOrDefault("ChapterNum",0) : 0) < minChapterNum)
            NetworkisGenerated = false;
        else if (useRandomAppear && new Random(RandomID).NextDouble() > (double)appearRate) NetworkisGenerated = false;
    }
}
public static class PropDatabase { [MethodImpl(MethodImplOptions.NoInlining)] public static MysticPot FindPropById(string id) => new(); }
public class HiddenRoomRewardSpawner : Mirror.NetworkBehaviour
{
    public int Seed, Roll;
    public MysticPot Spawned;
    public bool Throw;
    [MethodImpl(MethodImplOptions.NoInlining)] public override void OnStartServer()
    {
        base.OnStartServer();
        var random = new Random(Seed); Roll = random.Next(0,5);
        if (Roll == 4) SpawnProp(PropDatabase.FindPropById("MysticPot"), random);
        if (Throw) throw new InvalidOperationException("fixture");
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private void SpawnProp(MysticPot pot, Random random)
    { Spawned = pot; pot.RandomID = random.Next(); Mirror.NetworkServer.Spawn(pot); }
}
