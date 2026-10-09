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
        }

        internal static void BeforeDeath(UnitAvatar __instance, DamageInstance diedFrom, out DeathNotice __state)
        {
            __state = default;
            if (!NetworkServer.active || __instance.IsDead || !current.Friendly ||
                current.Victim != __instance || !ReferenceEquals(current.Damage, diedFrom) || !current.Attacker) return;
            try
            {
                // Death callbacks can clear names/ownership, destroy avatars or
                // reuse pooled damage. Snapshot only on the death boundary.
                __state = new DeathNotice { Killer = NoticeName(current.Attacker), Victim = NoticeName(__instance) };
            }
            catch (Exception error) { Warn(error); }
        }

        internal static void AfterDeath(UnitAvatar __instance, DeathNotice __state)
        {
            if (__state.Killer == null || !__instance.IsDead) return;
            try
            {
                var dungeon = DungeonManager.Instance;
                if (!NetworkServer.active || !NetworkClient.active || !dungeon || !dungeon.isServer) return;
                dungeon.Chat(null, "SephiriaOne", L.F("Friendly fire: {0} killed {1}.", __state.Killer, __state.Victim));
            }
            catch (Exception error) { Warn(error); }
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
