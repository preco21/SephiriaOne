using System.Runtime.CompilerServices;
namespace Mirror { public static class NetworkServer { public static bool active = true; } }
namespace UnityEngine { public static class Debug { public static readonly List<object> Warnings = new(); public static void LogWarning(object message) => Warnings.Add(message); } }
namespace SephiriaOne
{
    internal static class SessionSettings
    {
        internal static EventSpawnSettings Current;
        internal static bool Throw;
        internal static EventSpawnSettings EventSpawnsForGeneration => Throw ? throw new Exception("fixture unavailable scope") : Current;
    }
}
public class FloorData { public int randomRoomCount, seed; }
public class StageEntity_Choice
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public FloorData[] GenerateStage(int seed)
    {
        var random = new Random(seed);
        int randomRoomCount = 0;
        double roll = new Random(random.Next() + 10000).NextDouble();
        if (roll <= 0.003) randomRoomCount = 2;
        else if (roll <= 0.043) randomRoomCount = 1;
        return new[] { new FloorData { randomRoomCount = randomRoomCount, seed = random.Next() } };
    }
}
public class StageEntity_GrasslandTown
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public FloorData[] GenerateStage(int seed)
    {
        var random = new Random(seed);
        int randomRoomCount = 0;
        double roll = new Random(random.Next() + 10000).NextDouble();
        if (roll <= 0.003) randomRoomCount = 2;
        else if (roll <= 0.043) randomRoomCount = 1;
        return new[] { new FloorData { randomRoomCount = randomRoomCount, seed = random.Next() } };
    }
}
