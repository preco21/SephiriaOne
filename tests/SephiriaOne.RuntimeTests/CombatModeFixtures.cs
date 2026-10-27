using System.Runtime.CompilerServices;
public partial class UnitAvatar
{
    public sbyte isInBattle;
    public bool IsInBattle => isInBattle > 0;
    public int BattleWrites, StartedBattles, StoppedBattles;
    public Action NativeBattleCallback;
    public sbyte NetworkisInBattle { get => isInBattle; set { isInBattle = value; BattleWrites++; } }
    [MethodImpl(MethodImplOptions.NoInlining)] private void LocalStartBattle()
    {
        if (isInBattle == 0) { StartedBattles++; NativeBattleCallback?.Invoke(); }
        NetworkisInBattle = (sbyte)(isInBattle + 1);
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private void LocalStopBattle()
    {
        NetworkisInBattle = (sbyte)(isInBattle - 1);
        if (isInBattle == 0) { StoppedBattles++; NativeBattleCallback?.Invoke(); }
    }
    public void StartBattle() => LocalStartBattle();
    public void StopBattle() => LocalStopBattle();
}
public sealed partial class PlayerAvatar
{
    public int MapTrips;
    [MethodImpl(MethodImplOptions.NoInlining)] public void MoveFloorViaWorldmap(string guid, bool delay, string spawn) { MapTrips++; }
}
