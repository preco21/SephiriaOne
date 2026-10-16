using System.Runtime.CompilerServices;

namespace UnityEngine
{
    public static class Mathf
    {
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
    public bool EvasionDisabled;
    public Action<DamageInstance> OnEvade;
}
