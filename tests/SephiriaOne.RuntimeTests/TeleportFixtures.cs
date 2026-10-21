namespace UnityEngine
{
    public readonly record struct Vector3(float x, float y, float z);
    public sealed class Transform { public Vector3 position; }
}
namespace Mirror
{
    public static partial class NetworkServer { public static NetworkConnectionToClient localConnection; }
}
public sealed partial class DungeonManager { public bool IsHostTraveling; }
public sealed partial class PlayerAvatar
{
    public readonly UnityEngine.Transform transform = new();
    public int loadingScreenType = -1;
    public readonly List<UnityEngine.Vector3> Teleports = new();
    public Action OnTeleport;
    public void ReqSetPosition(UnityEngine.Vector3 position, bool teleport)
    {
        if (!teleport) throw new Exception("Expected native teleport flag.");
        Teleports.Add(position); OnTeleport?.Invoke();
    }
}
