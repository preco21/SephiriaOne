using System.Runtime.CompilerServices;

public partial class UnitAvatar
{
    public delegate float CalculateDurabilityDamage(float amount);
    public CalculateDurabilityDamage OnCalculateDurabilityDamage;
    public int GuardResistance;
    public bool PerfectGuard, InfiniteMp, GuardBroken;
    public int LastMpCost;
    public int MaxMp { get; set; } = 100;
    [MethodImpl(MethodImplOptions.NoInlining)] public void UseMp(int amount)
    {
        LastMpCost = amount;
        if (!InfiniteMp) Mp = Math.Max(0, Mp - amount);
    }
}
