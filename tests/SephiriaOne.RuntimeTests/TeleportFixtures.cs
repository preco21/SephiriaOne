namespace UnityEngine
{
    public readonly record struct Vector3(float x, float y, float z);
    public sealed class Transform { public Vector3 position; }
}
namespace Mirror
{
    public static partial class NetworkServer { public static NetworkConnectionToClient localConnection; }
}
public sealed class FloorGenerator : UnityEngine.Object
{
    public bool isServer = true;
    public uint netId;
    public static readonly Dictionary<string, FloorGenerator> Floors = new();
    public bool GenerateSuccess = true;
    public FloorData DataOnServer;
    public static FloorGenerator FindByGuid(string guid) => Floors.GetValueOrDefault(guid);
}
public sealed partial class DungeonManager
{
    public bool IsHostTraveling;
    private sealed class TravelingTransaction
    {
        public PlayerAvatar avatar;
        public string floorGuid, spawnPoint;
        public UnityEngine.Vector3? overridePosition;
    }
    private readonly List<TravelingTransaction> travelingTransactions = new();
    public readonly List<PlayerAvatar> TravelRequests = new();
    public int PendingTravelCount => travelingTransactions.Count;
    public Action<PlayerAvatar> OnTravel;
    public void MoveFloor(PlayerAvatar avatar, string floorGuid, string spawnPoint, int loadingScreen,
        bool recordHistory = true, bool allowSave = true, bool keepPrevFloor = false, bool randomPosition = false,
        bool leaveTrainingSchool = false, UnityEngine.Vector3? overridePosition = null)
    {
        if (loadingScreen != 0 || !recordHistory || allowSave || keepPrevFloor || randomPosition || leaveTrainingSchool || !overridePosition.HasValue)
            throw new Exception("Recovery must preserve native travel and use a fixed position without saving or randomizing");
        travelingTransactions.Add(new() { avatar = avatar, floorGuid = floorGuid, spawnPoint = spawnPoint, overridePosition = overridePosition });
        TravelRequests.Add(avatar); avatar.loadingScreenType = loadingScreen; OnTravel?.Invoke(avatar);
    }
    public void CompleteTravel()
    {
        for (int i = travelingTransactions.Count - 1; i >= 0; i--)
        {
            var travel = travelingTransactions[i];
            if (!travel.avatar) { travelingTransactions.RemoveAt(i); continue; }
            var floor = FloorGenerator.FindByGuid(travel.floorGuid);
            if (!floor || !floor.GenerateSuccess) continue;
            travelingTransactions.RemoveAt(i);
            travel.avatar.ReqSetPosition(travel.overridePosition.Value, true);
            travel.avatar.loadingScreenType = -1;
            travel.avatar.currentFloorGuid = travel.floorGuid;
            travel.avatar.currentSpawnPoint = travel.spawnPoint;
        }
    }
}
public sealed partial class PlayerAvatar
{
    public readonly UnityEngine.Transform transform = new();
    public int loadingScreenType = -1;
    public bool MovingFloorViaWorldmap { get; set; }
    public string currentSpawnPoint = "FLOORSTARTING";
    public readonly List<UnityEngine.Vector3> Teleports = new();
    public Action OnTeleport;
    public void ReqSetPosition(UnityEngine.Vector3 position, bool teleport)
    {
        if (!teleport) throw new Exception("Expected native teleport flag.");
        Teleports.Add(position); OnTeleport?.Invoke();
    }
}
