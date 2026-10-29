using System.Runtime.CompilerServices;

public enum ECustomStat { HPSteal, MPSteal, GuardResist, Evasion, FinalMP, AllDamageBonus, CriticalDamageBonus, PhysicalDamage, FireDamage, IceDamage, LightningDamage }

public partial class UnitAvatar
{
    public int HpHarvest, MpHarvest, MpRegenMultiple, HealingPenalty, HarvestReceivedIncrease;
    public float MpStack;
    public Action OnHeal;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int GetCustomStat(ECustomStat stat) => (int)stat >= (int)ECustomStat.PhysicalDamage ? ConvertedElementForTest((int)stat - (int)ECustomStat.PhysicalDamage) : stat == ECustomStat.HPSteal ? HpHarvest : stat == ECustomStat.MPSteal ? MpHarvest : stat == ECustomStat.Evasion ? Evasion : stat == ECustomStat.FinalMP ? FinalMp : stat == ECustomStat.CriticalDamageBonus ? CriticalDamageBonus : stat == ECustomStat.AllDamageBonus ? AllDamageBonus : GuardResistance;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Heal(float amount)
    {
        if (!IsDead) Hp = Math.Min(MaxHp, Hp + amount * Math.Max(0, 1 - HealingPenalty / 100f));
        OnHeal?.Invoke();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HealMpFloat(float amount)
    {
        MpStack += amount * (100 + MpRegenMultiple) / 100f;
        if (MpStack > 1)
        {
            int whole = (int)MpStack;
            Mp = Math.Min(100, Mp + whole); MpStack -= whole;
        }
    }
}
