using System;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static int tournamentTimeShown = -1;
        private static long tournamentMessageRevision;
        private static RosterEntry tournamentFirstNotice, tournamentSecondNotice;

        private static void ShowTournamentStatus(double now)
        {
            int seconds = (int)Math.Ceiling(endsAt - now);
            if (seconds <= 0 || seconds == tournamentTimeShown) return;
            tournamentTimeShown = seconds;
            SendTournamentMessages("[Tournament] Opponent: \"{0}\" | {1}s left", seconds, 0);
        }

        private static void SendTournamentMessages(string format, int seconds, int round)
        {
            if (first < 0 || second < 0 || !IsTournament) return;
            var a = tournamentPlayers[first]; var b = tournamentPlayers[second];
            long current = token, revision = tournamentMessageRevision;
            Phase expected = phase;
            if (!CanSendTournamentMessages(a, b, current, revision, expected)) return;
            // Register ownership before native callbacks; a nested death/stop
            // can replace the message already sent and cancel the other send.
            tournamentFirstNotice = a;
            Broadcast(a.Subject.Player, L.F(format, CombatNames.Player(b.Subject.Player), seconds, round));
            if (!CanSendTournamentMessages(a, b, current, revision, expected)) return;
            tournamentSecondNotice = b;
            Broadcast(b.Subject.Player, L.F(format, CombatNames.Player(a.Subject.Player), seconds, round));
        }

        private static bool CanSendTournamentMessages(RosterEntry a, RosterEntry b, long current, long revision, Phase expected) =>
            IsTournament && current == token && revision == tournamentMessageRevision && phase == expected &&
            CurrentScope && first >= 0 && second >= 0 && !firstDied && !secondDied &&
            ReferenceEquals(tournamentPlayers[first], a) && ReferenceEquals(tournamentPlayers[second], b) &&
            Connected(a.Subject, a.Receipt) && Connected(b.Subject, b.Receipt) &&
            a.Subject.IsReady && b.Subject.IsReady && !a.Subject.Player.IsDead && !b.Subject.Player.IsDead;

        private static void ForgetTournamentMessages()
        {
            tournamentFirstNotice = tournamentSecondNotice = null;
            tournamentTimeShown = -1;
            tournamentMessageRevision++;
        }

        private static void ClearTournamentMessages()
        {
            var a = tournamentFirstNotice; var b = tournamentSecondNotice;
            tournamentTimeShown = -1;
            long current = token, revision = ++tournamentMessageRevision;
            // Stock Chat AND RpcChat reject empty/whitespace messages. A short
            // terminal notice replaces the old clock on unmodified guests and
            // fades naturally. No custom RPC, invisible text or client patch.
            // Detach each recipient only immediately before sending. If the
            // first send stops the match, nested cleanup still owns the second.
            tournamentFirstNotice = null;
            ReplaceTournamentMessage(a, current, revision);
            if (current != token || revision != tournamentMessageRevision) return;
            tournamentSecondNotice = null;
            ReplaceTournamentMessage(b, current, revision);
        }

        private static void ReplaceTournamentMessage(RosterEntry entry, long current, long revision)
        {
            if (entry != null && current == token && revision == tournamentMessageRevision &&
                CurrentScope && Connected(entry.Subject, entry.Receipt))
                Broadcast(entry.Subject.Player, L.T("[Tournament] Match ended."));
        }
    }
}
