using System;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Read-only views subscribe here instead of teaching every command which
        // UI surfaces display its policy. Never use this event to replay gameplay.
        internal static event Action SettingsChanged;

        private static void NotifySettingsChanged()
        {
            if (SettingsChanged == null) return;
            foreach (Action listener in SettingsChanged.GetInvocationList())
            {
                try { listener(); }
                catch (Exception error)
                { Debug.LogWarning("[SephiriaOne] Settings view refresh failed: " + error); }
            }
        }
    }
}
