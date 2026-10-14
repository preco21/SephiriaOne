using System;
using Mirror;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        internal struct DeathNotice
        {
            internal string Killer, Victim;
            internal string ReliableKiller;
            internal FriendlyFireKda.Death Kda;
            internal long ScoreEpoch;
            internal long ParticipantEpoch;
            internal bool MatchDeath;
            internal FriendlyFireParticipants.Receipt PlayerReceipt;
            internal long PresentationEpoch;
        }

        internal static void BeforeDeath(UnitAvatar __instance, DamageInstance diedFrom, out DeathNotice __state)
        {
            __state = default;
            if (!NetworkServer.active || __instance.IsDead) return;
            if (__instance is PlayerAvatar dying) __state.MatchDeath = CombatMatchRuntime.EnterDeath(dying);
            try
            {
                if (__instance is PlayerAvatar visual && SessionSettings.FriendlyFireForHit.Enabled &&
                    FriendlyFireParticipants.Includes(visual))
                {
                    __state.PlayerReceipt = FriendlyFireParticipants.Capture(visual);
                    __state.PresentationEpoch = FriendlyFireParticipants.Epoch;
                }
                RememberCompanionOwner(__instance);
                bool attributed = CurrentHitPermitted() && current.Victim == __instance &&
                    ReferenceEquals(current.Damage, diedFrom) && current.Attacker && current.KdaEpoch == FriendlyFireKda.Epoch;
                if (__instance is PlayerAvatar victim)
                    __state.Kda = FriendlyFireKda.BeforeDeath(victim, attributed ? current.Attacker : null, current.KdaEpoch);
                if (!attributed) return;
                __state.ScoreEpoch = current.KdaEpoch;
                __state.ParticipantEpoch = FriendlyFireParticipants.Epoch;
                // Death callbacks can clear names/ownership, destroy avatars or
                // reuse pooled damage. Snapshot only on the death boundary.
                __state.Killer = NoticeName(current.Attacker); __state.Victim = NoticeName(__instance);
                __state.ReliableKiller = NativePlayerChat.ReadableName(current.Attacker.Name);
                if (!(__instance is PlayerAvatar)) __state.Killer = FriendlyFireKda.Label(current.Attacker, __state.Killer);
            }
            catch (Exception error) { Warn(error); }
        }

        internal static void AfterDeath(UnitAvatar __instance, DeathNotice __state)
        {
            if (!NetworkServer.active || !__instance || !__instance.IsDead) return;
            try
            {
                // Deliver before match callbacks can advance the round. The receipt
                // rejects callbacks that retired/replaced this connection or scope.
                if (__state.PlayerReceipt != null && SessionSettings.FriendlyFireForHit.Enabled &&
                    FriendlyFireParticipants.IsCurrent(__state.PlayerReceipt) &&
                    __state.PresentationEpoch == FriendlyFireParticipants.Epoch &&
                    __instance is PlayerAvatar visual && FriendlyFireParticipants.Includes(visual))
                {
                    // Also isolate loader/JIT failures before the optional
                    // adapter's own try/catch from match recovery and KDA.
                    try { FriendlyFireDeathVisuals.Show(visual); }
                    catch (Exception error) { Warn(error); }
                    try { FriendlyFireDeathAudio.Show(visual); }
                    catch (Exception error) { Warn(error); }
                }
                if (__instance is PlayerAvatar dead) CombatMatchRuntime.Died(dead);
                // A reset/toggle/run transition invalidates every old notice,
                // including killer totals captured for NPC/companion victims.
                if (__state.Killer != null && (!CurrentHitPermitted() || __state.ScoreEpoch != FriendlyFireKda.Epoch ||
                    __state.ParticipantEpoch != FriendlyFireParticipants.Epoch)) return;
                if (__instance is PlayerAvatar victim && FriendlyFireKda.CompleteDeath(victim, __state.Kda))
                {
                    if (__state.ReliableKiller != null && FriendlyFireParticipants.IsCurrent(__state.PlayerReceipt) &&
                        __state.PresentationEpoch == FriendlyFireParticipants.Epoch)
                    {
                        // Private chat preserves "Me" as the victim's perspective
                        // and does not replace the match respawn-countdown bubble.
                        try
                        {
                            NativePlayerChat.Show(victim, L.F("Killed by ‘{0}’ (Matchup: Me {1}–{2} Opponent)",
                                __state.ReliableKiller, FriendlyFireKda.KillsAgainst(__state.Kda.Victim, __state.Kda.Killer),
                                FriendlyFireKda.KillsAgainst(__state.Kda.Killer, __state.Kda.Victim)));
                        }
                        catch (Exception error) { Warn(error); }
                    }
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
        {
            // An exceptional death may never have reached the native RpcDie.
            if (__exception != null) __state.PlayerReceipt = null;
            try { AfterDeath(__instance, __state); }
            finally { if (__instance is PlayerAvatar player) CombatMatchRuntime.LeaveDeath(player, __state.MatchDeath); }
            return __exception;
        }

        internal static void BeforeRevive(UnitAvatar __instance)
        {
            if (NetworkServer.active && __instance.IsDead && __instance is PlayerAvatar player)
            { ResetBalanceLife(player); FriendlyFireKda.ForgetLife(player); CombatMatchRuntime.Reviving(player); }
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

        internal static string SafeName(string name) => CombatNames.Safe(name);
    }
}
