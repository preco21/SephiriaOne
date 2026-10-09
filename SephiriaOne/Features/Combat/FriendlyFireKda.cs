using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace SephiriaOne
{
    // Host-only session scores. Weak life keys never retain disconnected avatars;
    // account totals contain no Unity objects and are discarded with the session.
    internal static class FriendlyFireKda
    {
        internal sealed class Score
        {
            internal long Kills, Deaths, Assists;
            internal string Label(string name) => name + "(" + Kills.ToString(CultureInfo.InvariantCulture) + "/" +
                Deaths.ToString(CultureInfo.InvariantCulture) + "/" + Assists.ToString(CultureInfo.InvariantCulture) + ")";
        }
        internal sealed class Life
        {
            internal readonly HashSet<Score> Contributors = new HashSet<Score>();
            internal bool Finished;
        }
        internal struct Death
        {
            internal long Epoch;
            internal Score Killer, Victim;
            internal Life Life;
        }
        private static readonly Dictionary<ulong, Score> accounts = new Dictionary<ulong, Score>();
        private static ConditionalWeakTable<UnityEngine.Object, Score> offline = new ConditionalWeakTable<UnityEngine.Object, Score>();
        private static ConditionalWeakTable<PlayerAvatar, Life> lives = new ConditionalWeakTable<PlayerAvatar, Life>();
        private static bool enabled;
        internal static long Epoch { get; private set; }

        internal static void SetEnabled(bool value)
        {
            if (enabled == value) return;
            Reset(); enabled = value;
        }
        internal static void Reset()
        {
            enabled = false; accounts.Clear(); offline = new ConditionalWeakTable<UnityEngine.Object, Score>();
            ClearLives();
        }
        internal static void ClearLives() { lives = new ConditionalWeakTable<PlayerAvatar, Life>(); Epoch++; }
        internal static void ForgetLife(PlayerAvatar player) { if (!ReferenceEquals(player, null)) lives.Remove(player); }

        private static Score For(PlayerAvatar player)
        {
            var spawner = player.spawner;
            bool owns = spawner && ReferenceEquals(spawner.PlayerAvatar, player);
            ulong account = owns ? spawner.steamID : 0;
            if (account == 0) return offline.GetValue(owns ? (UnityEngine.Object)spawner : player, _ => new Score());
            if (!accounts.TryGetValue(account, out Score score)) accounts.Add(account, score = new Score());
            return score;
        }
        internal static string Label(PlayerAvatar player, string name) => For(player).Label(name);

        internal static void Damage(PlayerAvatar attacker, PlayerAvatar victim, long epoch)
        {
            if (!enabled || epoch != Epoch || !attacker || !victim || attacker == victim || victim.IsDead) return;
            Score source = For(attacker), target = For(victim);
            if (ReferenceEquals(source, target)) return;
            lives.GetValue(victim, _ => new Life()).Contributors.Add(source);
        }

        internal static Death BeforeDeath(PlayerAvatar victim, PlayerAvatar killer, long epoch)
        {
            if (!victim || victim.IsDead) return default;
            if (!enabled || epoch != Epoch || !killer || killer == victim)
            {
                lives.TryGetValue(victim, out Life previous);
                return new Death { Epoch = Epoch, Life = previous };
            }
            Score source = For(killer), target = For(victim);
            return new Death { Epoch = Epoch, Killer = ReferenceEquals(source, target) ? null : source, Victim = target,
                Life = lives.GetValue(victim, _ => new Life()) };
        }

        internal static bool CompleteDeath(PlayerAvatar victim, Death death)
        {
            if (death.Epoch != Epoch || death.Life == null || death.Life.Finished || !victim.IsDead) return false;
            death.Life.Finished = true;
            // Remove this life only; a nested transition may have replaced it.
            if (lives.TryGetValue(victim, out Life current) && ReferenceEquals(current, death.Life)) lives.Remove(victim);
            if (!enabled || death.Killer == null || death.Victim == null) return false;
            death.Killer.Kills++; death.Victim.Deaths++;
            foreach (Score contributor in death.Life.Contributors)
                if (!ReferenceEquals(contributor, death.Killer) && !ReferenceEquals(contributor, death.Victim)) contributor.Assists++;
            death.Life.Contributors.Clear();
            return true;
        }
    }
}
