using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static bool tournamentMode, firstDied, secondDied;
        private static readonly List<RosterEntry> tournamentPlayers = new List<RosterEntry>();
        private static readonly Dictionary<PlayerAvatar, RosterEntry> tournamentRoster =
            new Dictionary<PlayerAvatar, RosterEntry>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Random tournamentRandom = new Random();
        private static TournamentSchedule schedule;
        private static int first = -1, second = -1;
        internal static bool IsTournament => tournamentMode && IsRunning;
        internal static bool CanStartTournament => CanStart && EligibleTournamentCount() >= 2;
        internal static bool StartTournament(out string message) => Start(true, out message);

        private static int EligibleTournamentCount()
        {
            int count = 0;
            foreach (var spawner in PlayerSpawner.MultiplayerList)
                if (spawner && spawner.PlayerAvatar && FriendlyFireParticipants.Includes(spawner.PlayerAvatar)) count++;
            return count;
        }

        private static bool CaptureTournament(out string message)
        {
            ClearTournament();
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || !FriendlyFireParticipants.Includes(spawner.PlayerAvatar)) continue;
                var player = spawner.PlayerAvatar;
                if (tournamentRoster.ContainsKey(player)) continue;
                var entry = new RosterEntry { Subject = new HostPlayer(spawner), Receipt = FriendlyFireParticipants.Capture(player) };
                tournamentPlayers.Add(entry); tournamentRoster.Add(player, entry);
            }
            message = L.T("Tournament requires at least two eligible players.");
            if (tournamentPlayers.Count < 2) { ClearTournament(); return false; }
            schedule = new TournamentSchedule(tournamentPlayers.Count, tournamentRandom.Next);
            return true;
        }

        private static bool TournamentEnrolled(PlayerAvatar player) =>
            tournamentRoster.TryGetValue(player, out var entry) && Connected(entry.Subject, entry.Receipt);

        // Shared by direct damage, debuffs, reflection, artifacts and companions.
        // Do not edit the host's selection just to restrict the current round.
        internal static bool AllowsPair(PlayerAvatar attacker, PlayerAvatar victim)
        {
            if (!tournamentMode || !IsRunning) return true;
            if (phase != Phase.Active || !CurrentScope || Now >= endsAt || first < 0 || second < 0 ||
                !attacker || !victim || ReferenceEquals(attacker, victim)) return false;
            var a = tournamentPlayers[first]; var b = tournamentPlayers[second];
            return Connected(a.Subject, a.Receipt) && Connected(b.Subject, b.Receipt) &&
                (ReferenceEquals(attacker, a.Subject.Player) && ReferenceEquals(victim, b.Subject.Player) ||
                 ReferenceEquals(attacker, b.Subject.Player) && ReferenceEquals(victim, a.Subject.Player));
        }

        // Pruning a selected connection is a selection change too, even if no UI
        // command was executed. Shared stop contains native recovery reentrancy.
        internal static void SelectionChanged()
        {
            if (IsTournament) Stop(true, true);
        }

        private static void TournamentDeath(PlayerAvatar player)
        {
            if (phase != Phase.Active || first < 0 || second < 0 || Now >= endsAt) return;
            if (ReferenceEquals(player, tournamentPlayers[first].Subject.Player)) firstDied = true;
            if (ReferenceEquals(player, tournamentPlayers[second].Subject.Player)) secondDied = true;
        }

        private static void AdvanceTournament(double now)
        {
            if (!IsTournament || !CurrentScope || deathDepth != 0 || ReviveAllAction.IsExecuting) return;
            for (int i = 0; now >= nextScan && i < tournamentPlayers.Count; i++)
            {
                var entry = tournamentPlayers[i];
                if (schedule.IsEliminated(i) || Connected(entry.Subject, entry.Receipt)) continue;
                schedule.Withdraw(i);
                if (i == first || i == second) ClearTournamentPair();
            }
            if (phase == Phase.Active)
            {
                // Observe deaths even if a native callback was missed. A recorded
                // death survives a manual revive before this host update.
                var a = tournamentPlayers[first].Subject.Player;
                var b = tournamentPlayers[second].Subject.Player;
                // A death after the deadline cannot turn an expired draw into a
                // loss just because the host's next update arrived late.
                bool aDead = firstDied || now < endsAt && a.IsDead, bDead = secondDied || now < endsAt && b.IsDead;
                if (aDead || bDead || now >= endsAt)
                {
                    int loser = aDead == bDead ? -1 : aDead ? first : second;
                    schedule.Resolve(first, second, loser);
                    string result = loser < 0 ? L.T("[Tournament] Round unresolved; other eligible pairings will be considered.") :
                        L.F("[Tournament] {0} wins the round.", CombatNames.Player(loser == first ? b : a));
                    ClearTournamentPair();
                    Broadcast(null, result);
                }
                return;
            }
            if (phase == Phase.BetweenRounds)
            {
                if (!schedule.TryNext(out first, out second)) { Stop(true, true); return; }
                var a = tournamentPlayers[first]; var b = tournamentPlayers[second];
                // Existing respawn service recovers dead entrants before warmup.
                // Floor loading keeps the reserved pair without burning retries.
                if (!a.Subject.IsReady || !b.Subject.IsReady || a.Subject.Player.IsDead || b.Subject.Player.IsDead) return;
                startsAt = now + 3; endsAt = startsAt + duration; openingShown = 3;
                firstDied = secondDied = false; phase = Phase.Countdown;
                FriendlyFireParticipants.InvalidateCombat();
                SessionSettings.DeathmatchChanged();
                PairCountdown(3);
                return;
            }
            if (phase == Phase.Countdown)
            {
                var a = tournamentPlayers[first]; var b = tournamentPlayers[second];
                if (!a.Subject.IsReady || !b.Subject.IsReady || a.Subject.Player.IsDead || b.Subject.Player.IsDead)
                { phase = Phase.BetweenRounds; return; }
                int seconds = (int)Math.Ceiling(startsAt - now);
                if (seconds > 0)
                {
                    if (seconds != openingShown) { openingShown = seconds; PairCountdown(seconds); }
                    return;
                }
                phase = Phase.Active;
                endsAt = now + duration;
                SessionSettings.DeathmatchChanged();
                PairMessage(true, duration);
            }
        }

        private static void ClearTournamentPair()
        {
            phase = Phase.BetweenRounds; first = second = -1; firstDied = secondDied = false;
            FriendlyFireParticipants.InvalidateCombat();
            SessionSettings.DeathmatchChanged();
        }

        private static void PairCountdown(int seconds) => PairMessage(false, seconds);
        private static void PairMessage(bool started, int seconds)
        {
            if (first < 0 || second < 0 || !IsTournament) return;
            long current = token;
            PlayerAvatar a = tournamentPlayers[first].Subject.Player, b = tournamentPlayers[second].Subject.Player;
            string format = started ? "[Tournament] Match started: Kill \"{0}\"! ({1} secs)" :
                "[Tournament] \"{0}\" is your opponent! Fight in: {1}...";
            Broadcast(a, L.F(format, CombatNames.Player(b), seconds));
            if (current == token && IsTournament) Broadcast(b, L.F(format, CombatNames.Player(a), seconds));
        }

        private static void ClearTournament()
        {
            tournamentMode = false; schedule = null; tournamentPlayers.Clear(); tournamentRoster.Clear();
            first = second = -1; firstDied = secondDied = false;
        }

        internal static string DescribeTournament() => L.F("Tournament: {0}. Round duration: {1}s. Remaining: {2}s.",
            L.T(!IsTournament ? "off" : phase == Phase.Countdown ? "countdown" : phase == Phase.Active ? "active" : "waiting"),
            IsTournament ? duration : SessionSettings.TournamentDuration, IsTournament && phase != Phase.BetweenRounds ?
                Math.Max(0, (int)Math.Ceiling((phase == Phase.Countdown ? startsAt : endsAt) - Now)) : 0);
    }
}
