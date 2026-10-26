using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static readonly List<RosterEntry> zombiePlayers = new List<RosterEntry>();
        private static readonly Dictionary<PlayerAvatar, int> zombieRoster =
            new Dictionary<PlayerAvatar, int>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Random zombieRandom = new Random();
        private static ZombieSurvivalRound zombieRound;
        private static FriendlyFireKda.Score[] zombieScores;
        private static string[] zombieNames;
        private static string zombieOutcome;
        private static int zombieTimeShown = -1;
        private static int zombieStartingCount;
        internal static bool IsZombieSurvival => mode == MatchMode.ZombieSurvival && IsRunning;
        internal static bool CanStartZombie => CanStart && EligibleTournamentCount() >= 2;
        internal static bool StartZombie(out string message) => Start(MatchMode.ZombieSurvival, out message);

        private static bool CaptureZombie(out string message)
        {
            ClearZombie();
            CaptureRoster(zombiePlayers, zombieRoster);
            message = L.T("Zombie Survival requires at least two eligible players.");
            if (zombiePlayers.Count >= 2)
            {
                zombieStartingCount = Math.Min(SessionSettings.ZombieStartingCount, zombiePlayers.Count - 1);
                zombieNames = new string[zombiePlayers.Count]; zombieScores = new FriendlyFireKda.Score[zombiePlayers.Count];
                for (int i = 0; i < zombiePlayers.Count; i++) zombieNames[i] = CombatNames.Player(zombiePlayers[i].Subject.Player);
                return true;
            }
            ClearZombie(); return false;
        }

        private static bool ZombieEnrolled(PlayerAvatar player) => TryZombieIndex(player, out _);
        private static bool TryZombieIndex(PlayerAvatar player, out int index)
        {
            index = -1;
            return player && zombieRoster.TryGetValue(player, out index) && Connected(zombiePlayers[index].Subject, zombiePlayers[index].Receipt);
        }
        private static bool CurrentZombieCombat => IsZombieSurvival && phase == Phase.Active && !stopping && CurrentScope &&
            zombieRound != null && zombieRound.Humans > 0 && Now < endsAt;
        private static bool TryActiveZombieRole(PlayerAvatar player, out bool zombie)
        {
            zombie = false;
            if (!CurrentZombieCombat || !player || player.IsDead ||
                !TryZombieIndex(player, out int index) || zombieRound.IsPending(index)) return false;
            zombie = zombieRound.IsZombie(index);
            return true;
        }
        internal static bool IsActiveZombie(PlayerAvatar player) => TryActiveZombieRole(player, out bool zombie) && zombie;

        // Potion admission follows the faction even during death/pending revival.
        // Combat bonuses deliberately use the stricter, living-role lookup above.
        internal static bool BlocksZombieHpPotions(PlayerAvatar player) => SessionSettings.ZombieBlockHpPotions &&
            SessionSettings.FriendlyFireForHit.Enabled && CurrentZombieCombat &&
            TryZombieIndex(player, out int index) && zombieRound.IsZombie(index);

        internal static string ZombieFactionMarker(FriendlyFireParticipants.Receipt receipt) =>
            zombieRound != null && CurrentZombieReceipt(receipt, token, out int index)
                ? zombieRound.IsZombie(index) ? "[Z]" : "[H]" : "";

        // Presentation remains attached during death/pending conversion; combat
        // bonuses and hostility still require successful revival above.
        internal static bool IsZombieVisualCurrent(PlayerAvatar player, FriendlyFireParticipants.Receipt receipt, long match) =>
            !stopping && phase == Phase.Active && CurrentZombieReceipt(receipt, match, out int index) &&
            ReferenceEquals(receipt.Player, player) && zombieRound != null && zombieRound.IsZombie(index) && zombiePlayers[index].Subject.IsReady;

        private static bool ZombieAllowsPair(PlayerAvatar attacker, PlayerAvatar victim) => CurrentZombieCombat &&
            TryZombieIndex(attacker, out int first) && TryZombieIndex(victim, out int second) && zombieRound.AllowsPair(first, second);

        private static void BeginZombie(double now)
        {
            // The activation notification can enter native callbacks. Recheck
            // readiness after it before selecting the initial, living Zombies.
            for (int i = 0; i < zombiePlayers.Count; i++)
            {
                var entry = zombiePlayers[i];
                if (!Connected(entry.Subject, entry.Receipt)) { Stop(true, true); return; }
                if (!entry.Subject.IsReady || entry.Subject.Player.IsDead) { phase = Phase.BetweenRounds; return; }
            }
            zombieRound = new ZombieSurvivalRound(zombiePlayers.Count, zombieStartingCount, zombieRandom);
            RetargetZombieBalance(now);
            endsAt = now + duration; RoleRevision++;
            var initialNames = new List<string>();
            var initialPlayers = new List<RosterEntry>();
            for (int i = 0; i < zombiePlayers.Count; i++)
            {
                var player = zombiePlayers[i].Subject.Player;
                zombieScores[i] = FriendlyFireKda.For(player);
                zombieNames[i] = CombatNames.Player(player);
                if (zombieRound.IsZombie(i)) { initialNames.Add(zombieNames[i]); initialPlayers.Add(zombiePlayers[i]); }
            }
            long current = token;
            for (int i = 0; i < initialPlayers.Count && IsZombieSurvival && current == token; i++)
            {
                UpdateZombieMovement(initialPlayers[i]);
                if (!IsZombieSurvival || current != token) return;
                ZombieRoleVisuals.Update(initialPlayers[i].Subject.Player);
            }
            if (!IsZombieSurvival || current != token) return;
            foreach (string message in ZombieSurvivalMessages.Starting(initialNames))
            {
                if (!IsZombieSurvival || current != token) return;
                Broadcast(null, message);
            }
            // Announcement callbacks can infect a Human or kill an initial Zombie.
            // Keep the opening recipients frozen and preserve their death bubbles.
            for (int i = 0; i < initialPlayers.Count && IsZombieSurvival && current == token; i++)
            {
                var entry = initialPlayers[i];
                if (Connected(entry.Subject, entry.Receipt) && IsActiveZombie(entry.Subject.Player))
                    Broadcast(entry.Subject.Player, L.T("[Z] You are a Zombie. Hunt [H] Humans to infect them!"));
            }
        }

        private static void ShowZombieCountdown(int seconds) => Broadcast(null, L.F("[Zombie Survival] Starts in: {0}s", seconds));

        private static void AdvanceZombie(double now)
        {
            if (!IsZombieSurvival || !CurrentScope || deathDepth != 0 || ReviveAllAction.IsExecuting) return;
            bool ready = true;
            for (int i = 0; i < zombiePlayers.Count; i++)
            {
                var entry = zombiePlayers[i];
                // Cancel instead of letting an essential role disappear or a new
                // connection inherit enrollment. Late joiners remain spectators.
                if (!Connected(entry.Subject, entry.Receipt)) { Stop(true, true); return; }
                if (!entry.Subject.IsReady || entry.Subject.Player.IsDead) ready = false;
            }
            if (zombieRound == null)
            {
                if (!ready) { phase = Phase.BetweenRounds; return; }
                if (phase == Phase.BetweenRounds)
                {
                    phase = Phase.Countdown; startsAt = now + 3; endsAt = startsAt + duration;
                    openingShown = 3; SessionSettings.DeathmatchChanged(); ShowZombieCountdown(3);
                }
                return;
            }
            // Only pre-deadline native deaths can infect. A final infection that
            // was recorded before expiry wins even if cleanup reaches this late.
            if (zombieRound.Humans == 0 || now >= endsAt)
            {
                zombieOutcome = zombieRound.Humans == 0 ? "[Zombie Survival] [Z] Zombies win!" : "[Zombie Survival] [H] Humans win!";
                Stop(true, true); return;
            }
            int remaining = (int)Math.Ceiling(endsAt - now);
            long current = token;
            for (int i = 0; i < zombiePlayers.Count && IsZombieSurvival && current == token; i++)
            {
                UpdateZombieMovement(zombiePlayers[i]);
                if (!IsZombieSurvival || current != token) return;
                if (zombieRound.IsZombie(i)) ZombieRoleVisuals.Update(zombiePlayers[i].Subject.Player);
            }
            if (!IsZombieSurvival || current != token) return;
            if (remaining != zombieTimeShown && (remaining <= 5 || remaining % 5 == 0))
            {
                zombieTimeShown = remaining;
                Broadcast(null, L.F("[Zombie Survival] {0}s left. [H] Humans: {1}; [Z] Zombies: {2}.", remaining, zombieRound.Humans, zombiePlayers.Count - zombieRound.Humans));
            }
        }

        private static bool CurrentZombieReceipt(FriendlyFireParticipants.Receipt receipt, long match, out int index)
        {
            index = -1;
            return IsZombieSurvival && CurrentScope && token == match && FriendlyFireParticipants.IsCurrent(receipt) &&
                TryZombieIndex(receipt.Player, out index) && ReferenceEquals(zombiePlayers[index].Receipt, receipt);
        }

        private static void ConfirmedZombieKill(FriendlyFireParticipants.Receipt killer, FriendlyFireParticipants.Receipt victim, long match, string killerName)
        {
            if (!CurrentZombieCombat || !CurrentZombieReceipt(killer, match, out int from) ||
                !CurrentZombieReceipt(victim, match, out int to) || !victim.Player.IsDead ||
                !zombieRound.IsZombie(from) || !zombieRound.AllowsPair(from, to)) return;
            InfectZombie(to, killerName ?? CombatNames.Player(killer.Player));
        }

        internal static void CompletedMatchDeath(PlayerAvatar player, long match, bool success, bool convertAnyDeath)
        {
            if (!IsZombieSurvival || !dying.TryGetValue(player, out var receipt) || !CurrentZombieReceipt(receipt, match, out int index)) return;
            if (!success) { Stop(true, true); return; }
            if (CurrentZombieCombat && player.IsDead && convertAnyDeath) InfectZombie(index);
        }

        private static void InfectZombie(int index, string infector = null)
        {
            if (!zombieRound.Infect(index)) return;
            RetargetZombieBalance(Now);
            if (pending.TryGetValue(zombiePlayers[index].Subject.Player, out var respawn))
            { respawn.ZombieConversion = true; respawn.ZombieInfector = infector; }
            FriendlyFireKda.ForgetContributions(zombiePlayers[index].Subject.Player);
            RoleRevision++;
            UpdateZombieMovement(zombiePlayers[index]);
            if (!IsZombieSurvival || zombieRound == null) return;
            ZombieRoleVisuals.Update(zombiePlayers[index].Subject.Player);
            // The shared death finalizer now sends the combined conversion and
            // respawn message once attribution/any-death policy is complete.
        }

        private static void ZombieRevived(FriendlyFireParticipants.Receipt receipt, long match)
        {
            if (!CurrentZombieReceipt(receipt, match, out int index) || zombieRound == null || receipt.Player.IsDead ||
                !zombieRound.Revived(index)) return;
            RoleRevision++;
            UpdateZombieMovement(zombiePlayers[index]);
        }

        private static List<string> ZombieResults()
        {
            var lines = new List<string>(zombiePlayers.Count + 1) { L.T(zombieOutcome ?? "[Zombie Survival] Match stopped.") };
            for (int i = 0; i < zombiePlayers.Count; i++)
            {
                var entry = zombiePlayers[i];
                if (Connected(entry.Subject, entry.Receipt)) zombieNames[i] = CombatNames.Player(entry.Subject.Player);
                var score = zombieScores[i];
                lines.Add(L.F("{0} - {1} ({2}/{3}/{4})", zombieNames[i], L.T(zombieRound != null && zombieRound.IsZombie(i) ? "[Z] Zombie" : "[H] Human"),
                    score?.Kills ?? 0, score?.Deaths ?? 0, score?.Assists ?? 0));
            }
            return lines;
        }

        private static void ClearZombie()
        {
            foreach (var entry in zombiePlayers) entry.Movement.Update(entry.Subject.Player, false);
            ZombieRoleVisuals.Clear();
            zombieBalance = default;
            zombieRound = null; zombieScores = null; zombieNames = null; zombieOutcome = null; zombieTimeShown = -1; zombieStartingCount = 0;
            zombiePlayers.Clear(); zombieRoster.Clear(); RoleRevision++;
        }

        private static void UpdateZombieMovement(RosterEntry entry) => entry.Movement.Update(entry.Subject.Player,
            IsZombieVisualCurrent(entry.Subject.Player, entry.Receipt, token));

        private static void RetargetZombieBalance(double now) =>
            zombieBalance.Retarget(zombiePlayers.Count - zombieRound.Humans, zombieRound.Humans, now);

        internal static string DescribeZombie() => L.F("Zombie Survival: {0}. Round: {1}s. Remaining: {2}s. [H] Humans: {3}; [Z] Zombies: {4}.",
            L.T(!IsZombieSurvival ? "off" : phase == Phase.Countdown ? "countdown" : phase == Phase.BetweenRounds ? "waiting" : "active"),
            IsZombieSurvival ? duration : SessionSettings.ZombieDuration,
            !IsZombieSurvival ? 0 : Math.Max(0, (int)Math.Ceiling((phase == Phase.Countdown ? startsAt : endsAt) - Now)),
            zombieRound?.Humans ?? 0, zombieRound == null ? 0 : zombiePlayers.Count - zombieRound.Humans);
    }
}
