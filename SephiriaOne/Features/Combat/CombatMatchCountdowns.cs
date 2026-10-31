using System;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        // A delayed frame emits only its current time, never a catch-up burst.
        // Role/pair changes reset their own last notice to -1.
        private static bool TakeCountdownNotice(int remaining, ref int shown)
        {
            if (remaining <= 0 || remaining == shown ||
                (shown >= 0 && remaining > 5 && shown - remaining < 5)) return false;
            shown = remaining; // Consume before native callbacks can reenter.
            return true;
        }

        private static void ShowCountdownText(PlayerAvatar player, string text)
        {
            try { NativePlayerFloatingText.Show(player, text); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Optional countdown presentation failed: " + error); }
        }
    }
}
