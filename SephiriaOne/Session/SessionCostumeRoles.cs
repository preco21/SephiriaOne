using System;
using System.Collections.Generic;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        private sealed class CostumeNotice
        {
            internal FriendlyFireParticipants.Receipt Receipt;
            internal (string Costume, long Policy, int Language, bool Potions, bool LevelUp, bool Collin) Observed;
            internal readonly StatSnapshot[] Stats = new StatSnapshot[companionRoleStats.Length];
            internal List<string> Lines;
            internal int NextLine;
            internal float RetryAt;
        }

        private static readonly StatDefinition[] companionRoleStats =
            new List<StatDefinition>(StatCatalog.All).FindAll(stat => stat.IsCompanion).ToArray();
        private static readonly Dictionary<HostPlayer, CostumeNotice> costumeNotices = new Dictionary<HostPlayer, CostumeNotice>();
        private static bool costumeNoticeWarned;

        private static void TryRefreshCostumeNotice(HostPlayer subject)
        {
            try { RefreshCostumeNotice(subject); }
            catch (Exception error)
            {
                if (costumeNoticeWarned) return;
                costumeNoticeWarned = true;
                Debug.LogWarning("[SephiriaOne] Costume role notice failed: " + error);
            }
        }

        // Reuse the session's enrolled subjects and connection receipts. This is
        // a read-only view after native-stat maintenance, never a gameplay rule.
        private static void RefreshCostumeNotice(HostPlayer subject)
        {
            string costume = subject.Player.currentCostume;
            if (!CostumeRoleText.Supported(costume)) { costumeNotices.Remove(subject); return; }
            if (!subject.IsReady) return;
            var receipt = FriendlyFireParticipants.Capture(subject.Player);
            if (receipt == null) { costumeNotices.Remove(subject); return; }
            if (!costumeNotices.TryGetValue(subject, out CostumeNotice notice) || !ReferenceEquals(notice.Receipt, receipt))
                costumeNotices[subject] = notice = new CostumeNotice { Receipt = receipt };

            bool companion = CostumeRoleText.Companion(costume);
            bool collin = companion && policy.CollinStartingArtifact && CollinFeature.Available && CollinFeature.HasStartingGift(subject.Player);
            var observed = (costume, intentRevision, L.Revision, RabbitPotionFeature.Available, RabbitLevelUpFeature.Available, collin);
            bool changed = notice.Lines == null || notice.Observed != observed;
            notice.Observed = observed;
            if (companion)
                for (int i = 0; i < companionRoleStats.Length; i++)
                {
                    StatSnapshot value = CaptureStat(subject.Player, companionRoleStats[i]);
                    changed |= !notice.Stats[i].Equals(value);
                    notice.Stats[i] = value;
                }
            if (changed)
            {
                List<string> parts = companion ? CostumeRoleText.CompanionParts(costume, collin) :
                    CostumeRoleText.Rabbit(policy.RabbitPotions, observed.Item4, observed.Item5, policy.FriendlyFire.BlockRabbitInfinite);
                if (companion)
                    foreach (StatSnapshot value in notice.Stats)
                        if (policy.TryDescribeAppliedCompanionStat(value, out string text)) parts.Add(text);
                List<string> lines = CostumeRoleText.Pack(parts);
                if (!SameLines(notice.Lines, lines))
                { notice.Lines = lines; notice.NextLine = 0; notice.RetryAt = 0; }
            }
            if (Time.unscaledTime < notice.RetryAt) return;
            while (notice.NextLine < notice.Lines.Count)
            {
                if (!FriendlyFireParticipants.IsCurrent(receipt) || !subject.IsReady ||
                    !NativePlayerChat.Show(subject.Player, notice.Lines[notice.NextLine]))
                { notice.RetryAt = Time.unscaledTime + 1; return; }
                notice.NextLine++;
            }
        }

        private static bool SameLines(List<string> a, List<string> b)
        {
            if (a == null || a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
