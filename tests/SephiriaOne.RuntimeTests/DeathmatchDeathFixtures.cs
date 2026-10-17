// Only damage attribution is stubbed here; production death finalizers, match
// state machine and native-shaped death/revival methods execute together.
namespace SephiriaOne
{
    internal static class PlayTagExplosion
    {
        internal static readonly List<PlayerAvatar> Shown = new();
        internal static bool Owned;
        internal static void Show(PlayerAvatar player) { Shown.Add(player); Owned = true; }
        internal static void Tick() { }
        internal static void Reset() { Owned = false; }
    }
    internal static class FriendlyFireDeathAudio
    {
        internal static int Shown;
        internal static void Reset() { Shown = 0; }
        internal static void Show(PlayerAvatar player) { Shown++; }
    }
    internal static class NativePlayerChat
    {
        internal static readonly List<(PlayerAvatar Player, string Message)> Messages = new();
        internal static string ReadableName(string name) => CombatNames.Safe(name) is var plain && plain != "?" ? plain : null;
        internal static bool Available = true;
        internal static bool Show(PlayerAvatar player, string message)
        { if (!Available) return false; Messages.Add((player, message)); return true; }
    }
    internal static class FriendlyFireDeathVisuals
    {
        internal static int Shown;
        internal static void Show(PlayerAvatar player) { Shown++; }
    }
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
        private static void RememberCompanionOwner(UnitAvatar source) { }
        // Full budget/life behavior executes in FriendlyFireTests with damage hooks.
        private static void ResetBalanceLife(PlayerAvatar player) { }
        private static bool CurrentHitPermitted() => current.Friendly && FriendlyFireParticipants.Includes(current.Attacker) &&
            current.Victim is PlayerAvatar victim && FriendlyFireParticipants.Includes(victim);
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
