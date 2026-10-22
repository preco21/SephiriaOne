using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    public class Component : Object
    {
        private readonly Transform nativeTransform = new();
        public Transform transform => Destroyed ? throw new NullReferenceException("orphaned native transform") : nativeTransform;
    }
    public class Transform { public int position = 42; }
    public static class Debug { public static readonly List<string> Warnings = new(); public static void LogWarning(object text) => Warnings.Add(text.ToString()); }
}
namespace Mirror
{
    public class NetworkBehaviour : UnityEngine.Component { public bool isServer { get; set; } = true; }
}
public class UnitAvatar : UnityEngine.Component { }
public class CharacterBuff : Mirror.NetworkBehaviour
{
    public UnitAvatar NetworkTarget { get; set; } = new();
    public bool IsEndBuff { get; set; }
    public int BaseCalls, CleanupCalls;
    public Action OnBase;
    // The fixture models managed base work, including callbacks that can retire
    // the Unity target before the derived native transform access. It does not
    // claim that the real base HUD calls tolerate an actual CLR-null target.
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected virtual void OnUpdate_Server() { BaseCalls++; OnBase?.Invoke(); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Tick() { OnUpdate_Server(); CleanupCalls++; }
}
public class CharacterBuff_StatusInstance : CharacterBuff { }
public class CharacterBuff_StatusInstance_FrostTouch : CharacterBuff_StatusInstance
{
    public int TimerCalls, FxCalls, LastPosition;
    public bool Due = true;
    private bool AdvanceTimer() { TimerCalls++; return Due; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override void OnUpdate_Server()
    {
        base.OnUpdate_Server();
        if (isServer && AdvanceTimer()) RpcCreateFrostTouchFx(NetworkTarget.transform.position);
    }
    public void RpcCreateFrostTouchFx(int position) { FxCalls++; LastPosition = position; }
}
namespace SephiriaOne
{
    internal static class HarmonyRuntime { internal static void EnsureLoaded() { } }
}
