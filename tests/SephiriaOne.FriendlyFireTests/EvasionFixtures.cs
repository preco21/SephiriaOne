using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class Mathf
    {
        public static int RoundToInt(float value) => (int)MathF.Round(value);
        [MethodImpl(MethodImplOptions.NoInlining)] public static float Log(float value) => MathF.Log(value);
    }

    public static class Random
    {
        public static float NextValue = 100;
        public static int Calls;
        [MethodImpl(MethodImplOptions.NoInlining)] public static float Range(float min, float max)
        { Calls++; return NextValue; }
    }
}

public partial class UnitAvatar
{
    public int Evasion, IgnoreEvasion, AbsoluteEvasion;
    public int EvasionRateReduction;
    public bool EvasionDisabled;
    public Action<DamageInstance> OnEvade;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public float GetEvasionChancePercent()
    {
        float stat = GetCustomStat(ECustomStat.Evasion);
        if (stat > 10000f) stat = 10000f;
        float chance = 100f * UnityEngine.Mathf.Log(stat / 6200f + 1f) * 0.8f;
        int modifier = GetCustomStatUnsafe("EVASIONRATEREDUCTION");
        if (modifier != 0) chance = chance * (float)(100 + modifier) / 100f;
        if (chance < 0f) chance = 0f;
        return chance;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public bool BetaEvasionRoll(UnitAvatar attacker)
    {
        float chance = GetEvasionChancePercent();
        int ignore = attacker.GetCustomStatUnsafe("IGNOREEVASION");
        if (ignore > 0) chance -= chance * ((float)ignore / 100f);
        return chance >= UnityEngine.Random.Range(0f, 100f);
    }
}
