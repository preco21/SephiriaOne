using System;
using System.Runtime.InteropServices;

namespace SephiriaOne
{
    // Own only the fields we replace. Native mutations run without this projection.
    internal sealed class PvpMaxHpProjection
    {
        private bool baseOwned, curseOwned, observed;
        private float nativeBase, appliedBase, seenBase, seenFinal;
        private int nativeCurse, appliedCurse, seenCurse;
        private sbyte seenCursed;
        private decimal baseline, soft, hard;

        internal void Apply(PlayerAvatar player, PvpMaxHpSettings settings)
        {
            if (!player) return;
            if (!settings.Enabled) { Release(player); return; }
            if (observed && player.maxHp == seenBase && player.finalMaxHp == seenFinal &&
                player.cursedMaxHp == seenCurse && player.isHPCursed == seenCursed &&
                baseline == settings.Baseline && soft == settings.Soft && hard == settings.Hard) return;
            Release(player);
            float before = player.MaxHp;
            if (!Positive(before)) return;
            float target = settings.Limit(before);
            if (!Positive(target) || target >= before) { Observe(player, settings); return; }
            if (player.isHPCursed > 0)
            {
                nativeCurse = player.cursedMaxHp;
                appliedCurse = Math.Max(1, (int)Math.Floor(target));
                curseOwned = true;
                player.NetworkcursedMaxHp = appliedCurse;
            }
            else
            {
                double factor = 1d + player.finalMaxHp / 100d;
                if (factor <= 0 || double.IsInfinity(factor) || double.IsNaN(factor) || !Positive(player.maxHp)) return;
                nativeBase = player.maxHp;
                appliedBase = (float)(target / factor);
                if (!Positive(appliedBase)) return;
                // Match the native getter's float evaluation; never exceed the configured ceiling by an ULP.
                for (int i = 0; i < 4 && Maximum(appliedBase, player.finalMaxHp) > target; i++)
                    appliedBase = NextDown(appliedBase);
                if (!Positive(Maximum(appliedBase, player.finalMaxHp)) || Maximum(appliedBase, player.finalMaxHp) > target) return;
                baseOwned = true;
                player.NetworkmaxHp = appliedBase;
            }
            ScaleCurrentHp(player, before, player.MaxHp);
            Observe(player, settings);
        }

        internal void Release(PlayerAvatar player)
        {
            observed = false;
            if (player && (baseOwned || curseOwned))
            {
                float before = player.MaxHp;
                if (baseOwned && player.maxHp == appliedBase) player.NetworkmaxHp = nativeBase;
                if (curseOwned && player.cursedMaxHp == appliedCurse) player.NetworkcursedMaxHp = nativeCurse;
                ScaleCurrentHp(player, before, player.MaxHp);
            }
            baseOwned = curseOwned = false;
        }

        internal float NativeHp(PlayerAvatar player)
        {
            if (!player) return 0;
            float nativeMax = player.isHPCursed > 0
                ? (curseOwned && player.cursedMaxHp == appliedCurse ? nativeCurse : player.cursedMaxHp)
                : Maximum(baseOwned && player.maxHp == appliedBase ? nativeBase : player.maxHp, player.finalMaxHp);
            return ScaledHp(player.hp, player.MaxHp, nativeMax);
        }

        internal bool HasNativeCurse(UnitAvatar player, int expected) => player && curseOwned &&
            player.isHPCursed > 0 && player.cursedMaxHp == appliedCurse && nativeCurse == expected;

        private void Observe(PlayerAvatar player, PvpMaxHpSettings settings)
        {
            observed = true; seenBase = player.maxHp; seenFinal = player.finalMaxHp;
            seenCurse = player.cursedMaxHp; seenCursed = player.isHPCursed;
            baseline = settings.Baseline; soft = settings.Soft; hard = settings.Hard;
        }
        private static float Maximum(float value, float percent) => value + percent * value / 100f;
        private static bool Positive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);
        private static float ScaledHp(float hp, float before, float after)
        {
            // Preserve dead/non-finite HP and native overheal; do not manufacture a revive or healing event.
            if (hp <= 0 || float.IsNaN(hp) || float.IsInfinity(hp) || !Positive(before) || !Positive(after) || before == after) return hp;
            double value = (double)hp * after / before;
            return value > float.MaxValue ? hp : (float)value;
        }
        private static void ScaleCurrentHp(PlayerAvatar player, float before, float after)
        {
            float value = ScaledHp(player.hp, before, after);
            if (value != player.hp && !float.IsNaN(value)) player.Networkhp = value;
        }
        [StructLayout(LayoutKind.Explicit)] private struct FloatBits
        { [FieldOffset(0)] public float Value; [FieldOffset(0)] public int Bits; }
        private static float NextDown(float value) => new FloatBits { Bits = new FloatBits { Value = value }.Bits - 1 }.Value;
    }
}
