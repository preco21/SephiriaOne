using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace SephiriaOne
{
    internal sealed class StatsPanelBinding : IPresentationBinding
    {
        private readonly UI_StatsPanel panel;
        private static readonly FieldInfo Player = typeof(UI_StatsPanel).GetField("playerAvatar", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RefreshMethod = typeof(UI_StatsPanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        public StatsPanelBinding(UI_StatsPanel panel) { this.panel = panel; }
        private UnitAvatar Avatar => Player?.GetValue(panel) as UnitAvatar;
        public bool IsAlive => panel;
        public bool IsActive => panel && panel.IsOpened && Avatar;
        public object Observe()
        {
            var avatar = Avatar;
            // Immutable, exact, order-independent snapshots of all three native
            // dictionaries prevent both partial notification order and hash collisions.
            return (avatar.GetInstanceID(), Snapshot(avatar.customStats), Snapshot(avatar.calculatedBonusStats), Snapshot(avatar.customStatsAmp));
        }
        private static string Snapshot(IEnumerable<KeyValuePair<string, int>> values)
        {
            var sorted = new List<KeyValuePair<string, int>>(values);
            sorted.Sort((left, right) => string.CompareOrdinal(left.Key, right.Key));
            var result = new StringBuilder();
            foreach (var pair in sorted) result.Append(pair.Key.Length).Append(':').Append(pair.Key).Append('=').Append(pair.Value).Append(';');
            return result.ToString();
        }
        public ReconcileResult Refresh()
        {
            if (RefreshMethod == null) return ReconcileResult.Rejected("native stat-only refresh unavailable");
            // Audited Sephiria 1.0.33: Refresh() only updates stat labels/categories;
            // OnOpened() additionally recreates controls and must never be replayed.
            RefreshMethod.Invoke(panel, null);
            return ReconcileResult.Applied();
        }
        public void Restore() { }
    }
}
