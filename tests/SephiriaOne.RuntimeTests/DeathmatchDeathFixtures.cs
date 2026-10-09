// Only damage attribution is stubbed here; production death finalizers, match
// state machine and native-shaped death/revival methods execute together.
namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private struct HitContext
        {
            internal bool Friendly;
            internal UnitAvatar Victim;
            internal DamageInstance Damage;
            internal PlayerAvatar Attacker;
            internal long KdaEpoch;
        }
        private static HitContext current;
        internal static void Attribute(PlayerAvatar killer, PlayerAvatar victim, DamageInstance damage) =>
            current = new HitContext { Friendly = true, Attacker = killer, Victim = victim, Damage = damage, KdaEpoch = FriendlyFireKda.Epoch };
        private static void Warn(Exception error) => UnityEngine.Debug.LogWarning(error);
    }
}
public sealed class DamageInstance { }
public partial class UnitAvatar
{
    public uint netId;
    public UnitAvatar NetworkLeader;
    public string Name => this is PlayerAvatar player ? player.playerNameSource : "";
    public Action<DamageInstance> DieCallback;
    public readonly List<string> Lifecycle = new();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Die(int hitLevel, DamageInstance diedFrom)
    {
        if (IsDead) return;
        IsDead = true; hp = 0;
        DieCallback?.Invoke(diedFrom);
        Lifecycle.Add("RpcDie");
    }
}
