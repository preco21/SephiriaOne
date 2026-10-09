using System;
using System.Text;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal struct DeathNotice
        {
            internal string Killer, Victim;
            internal FriendlyFireKda.Death Kda;
        }

        internal static void BeforeDeath(UnitAvatar __instance, DamageInstance diedFrom, out DeathNotice __state)
        {
            __state = default;
            if (!NetworkServer.active || __instance.IsDead) return;
            try
            {
                bool attributed = current.Friendly && current.Victim == __instance &&
                    ReferenceEquals(current.Damage, diedFrom) && current.Attacker && current.KdaEpoch == FriendlyFireKda.Epoch;
                if (__instance is PlayerAvatar victim)
                    __state.Kda = FriendlyFireKda.BeforeDeath(victim, attributed ? current.Attacker : null, current.KdaEpoch);
                if (!attributed) return;
                // Death callbacks can clear names/ownership, destroy avatars or
                // reuse pooled damage. Snapshot only on the death boundary.
                __state.Killer = NoticeName(current.Attacker); __state.Victim = NoticeName(__instance);
                if (!(__instance is PlayerAvatar)) __state.Killer = FriendlyFireKda.Label(current.Attacker, __state.Killer);
            }
            catch (Exception error) { Warn(error); }
        }

        internal static void AfterDeath(UnitAvatar __instance, DeathNotice __state)
        {
            if (!NetworkServer.active || !__instance.IsDead) return;
            try
            {
                // A toggle or run transition inside a death callback invalidates
                // the old encounter and its notice as well as its score receipt.
                if (__instance is PlayerAvatar && __state.Killer != null && __state.Kda.Epoch != FriendlyFireKda.Epoch) return;
                if (__instance is PlayerAvatar victim && FriendlyFireKda.CompleteDeath(victim, __state.Kda))
                {
                    __state.Killer = __state.Kda.Killer.Label(__state.Killer);
                    __state.Victim = __state.Kda.Victim.Label(__state.Victim);
                }
                if (__state.Killer == null) return;
                var dungeon = DungeonManager.Instance;
                if (!NetworkServer.active || !NetworkClient.active || !dungeon || !dungeon.isServer) return;
                dungeon.Chat(null, "SephiriaOne", L.F("Friendly fire: {0} killed {1}.", __state.Killer, __state.Victim));
            }
            catch (Exception error) { Warn(error); }
        }

        internal static Exception FinishDeath(Exception __exception, UnitAvatar __instance, DeathNotice __state)
        { AfterDeath(__instance, __state); return __exception; }

        internal static void BeforeRevive(UnitAvatar __instance)
        {
            if (NetworkServer.active && __instance.IsDead && __instance is PlayerAvatar player) FriendlyFireKda.ForgetLife(player);
        }

        private static string NoticeName(UnitAvatar avatar)
        {
            string name = SafeName(avatar.Name);
            if (name != "?") return name;
            if (avatar is PlayerAvatar player)
            {
                var spawner = player.spawner;
                return L.F("Player #{0}", spawner && ReferenceEquals(spawner.PlayerAvatar, player) && spawner.currentPlayerIdx >= 0
                    ? (long)spawner.currentPlayerIdx + 1 : player.netId);
            }
            if (avatar.NetworkLeader is PlayerAvatar owner && owner)
                return L.F("Companion of {0}", NoticeName(owner));
            return L.F("Unnamed ally #{0}", avatar.netId);
        }

        internal static string SafeName(string name)
        {
            var text = new StringBuilder(32);
            bool tag = false;
            foreach (char c in name ?? "")
            {
                if (c == '<') { tag = true; continue; }
                if (c == '>') { tag = false; continue; }
                if (!tag && !char.IsControl(c) && text.Length < 32) text.Append(c);
            }
            string plain = text.ToString().Trim();
            return plain.Length == 0 ? "?" : plain;
        }
    }
}
