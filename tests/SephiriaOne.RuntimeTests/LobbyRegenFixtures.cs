using System.Runtime.CompilerServices;

public sealed class StageEntity : UnityEngine.Object { public string name = "lobby"; }
public sealed class RaceEntity : UnityEngine.Object
{
    public StageEntity lobbyStage = new();
    public string[] healPlaces = { "town" };
}
public sealed class FloorData { public string stageName = "lobby", name = "town"; }
public sealed partial class DungeonManager
{
    public Dictionary<string, FloorData> generatedFloors = new() { ["town"] = new() };
}
public partial class UnitAvatar
{
    public int mp;
    public UnitAvatar Follower;
    public int UpdateCalls;
    private float hpRegenCounter, mpRegenCounter;
    private float Regen(string stat) => stat == "HPREGEN" ? 2 : 2;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Heal(float amount) => hp += amount;
    [MethodImpl(MethodImplOptions.NoInlining)] public void Heal(float amount, bool allowOverHeal, bool ignorePenalty) => hp += amount;
    [MethodImpl(MethodImplOptions.NoInlining)] public void HealMp(int amount) => mp += amount;
    [MethodImpl(MethodImplOptions.NoInlining)] protected virtual void Update()
    {
        hpRegenCounter += Regen("HPREGEN");
        if (hpRegenCounter > 1) { hpRegenCounter -= 1; Heal(1f); }
        mpRegenCounter += Regen("MPREGEN");
        if (mpRegenCounter > 1) { mpRegenCounter -= 1; HealMp(1); }
        UpdateCalls++;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public void TickRecovery() => Update();
}
public sealed partial class PlayerAvatar
{
    [MethodImpl(MethodImplOptions.NoInlining)] protected override void Update()
    {
        if (Follower) Follower.Heal(5f);
        if (Race && DungeonManager.Instance.generatedFloors.TryGetValue(currentFloorGuid, out var floor) &&
            Race.healPlaces.Contains(floor.name)) Heal(10f, false, false);
        base.Update();
    }
}
