using System.Runtime.CompilerServices;
using SephiriaOne;

public class UnitAvatar : UnityEngine.Object
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void AddCustomStat(ECustomStat stat, int amount)
    {
        var values = ((PlayerAvatar)this).customStats;
        values[stat.ToString().ToUpperInvariant()] = values.GetValueOrDefault(stat.ToString().ToUpperInvariant()) + amount;
    }
}
public enum ECustomStat { HPSteal = 6 }
public class StatusInstance
{
    private int value;
    private bool applied;
    protected UnitAvatar CurrentTarget { get; private set; }
    public int Value { [MethodImpl(MethodImplOptions.NoInlining)] get => value; }
    public void SetValue(int n) { if (!applied) value = n; }
    public void SetTarget(UnitAvatar target) => CurrentTarget = target;
    [MethodImpl(MethodImplOptions.NoInlining)] public void ClearTarget() => CurrentTarget = null;
    [MethodImpl(MethodImplOptions.NoInlining)] public void ApplyStatus(bool runtime)
    { if (!applied) { applied = true; ApplyStatusInner(runtime); } }
    [MethodImpl(MethodImplOptions.NoInlining)] public void RemoveStatus()
    { if (applied) { applied = false; RemoveStatusInner(); } }
    protected virtual void ApplyStatusInner(bool runtime) { }
    protected virtual void RemoveStatusInner() { }
}
public class StatusInstance_HPSteal : StatusInstance
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override void ApplyStatusInner(bool runtime) { base.ApplyStatusInner(runtime); CurrentTarget.AddCustomStat(ECustomStat.HPSteal, Value); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    protected override void RemoveStatusInner() { base.RemoveStatusInner(); CurrentTarget.AddCustomStat(ECustomStat.HPSteal, -Value); }
}
public static class StatusDatabase
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static StatusInstance CreateStatusEntity(string text)
    { var status = new StatusInstance_HPSteal(); status.SetValue(int.Parse(text.Split('/')[1])); return status; }
}
public sealed partial class PlayerAvatar
{
    public string currentCostume = "";
    private List<StatusInstance> costumeStats = new();
    public StatusInstance BatStatus => costumeStats.FirstOrDefault();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void UpdateCostumeData(string costumeID, bool fromRuntime)
    {
        foreach (var old in costumeStats) { old.RemoveStatus(); old.ClearTarget(); }
        costumeStats.Clear(); currentCostume = costumeID;
        foreach (var metadata in costumeID == "Bat" ? new[] { "HP_STEAL/5" } : Array.Empty<string>())
        {
            var status = StatusDatabase.CreateStatusEntity(metadata);
            status.SetTarget(this); status.ApplyStatus(fromRuntime); costumeStats.Add(status);
        }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private void OnDestroy() { }
    public void DestroyFixture() => OnDestroy();
}
namespace SephiriaOne
{
    internal static class BatCostumeFeature { internal static bool Available = true; }
}
