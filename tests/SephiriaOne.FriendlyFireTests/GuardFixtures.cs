using System.Runtime.CompilerServices;

public partial class UnitAvatar
{
    public delegate float CalculateDurabilityDamage(float amount);
    public CalculateDurabilityDamage OnCalculateDurabilityDamage;
    public int GuardResistance;
    public bool PerfectGuard, InfiniteMp, GuardBroken;
    public int LastMpCost;
    public int maxMp = 100, FinalMp, InfinityMaxMp = 1000;
    public int MaxMp
    {
        // Preserve the installed game's int-product-before-float order so
        // large Final MP stacks exercise the native overflow boundary.
        get => InfiniteMp ? InfinityMaxMp : unchecked(maxMp + (int)((float)(FinalMp * maxMp) / 100f));
        set { maxMp = value; FinalMp = 0; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)] public void UseMp(int amount)
    {
        LastMpCost = amount;
        if (!InfiniteMp) Mp = Math.Max(0, Mp - amount);
    }
}
