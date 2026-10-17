using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal static partial class CombatMatchRuntime
    {
        private static readonly List<RosterEntry> tagPlayers = new List<RosterEntry>();
        private static readonly Dictionary<PlayerAvatar, int> tagRoster =
            new Dictionary<PlayerAvatar, int>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Random tagRandom = new Random();
        private static PlayTagBomb tagBomb;
        private static PlayerAvatar tagExplosionVictim;
        private static string tagPreviousName;
        private static bool tagNaturalStop;
        private static int tagShown = -1;
        internal static long ChaserRevision { get; private set; }
        internal static void ChaserSettingsChanged() => ChaserRevision++;
        internal static bool IsPlayTag => mode == MatchMode.PlayTag && IsRunning;
        internal static bool CanStartPlayTag => CanStart && EligibleTournamentCount() >= 2;
        internal static bool StartPlayTag(out string message) => Start(MatchMode.PlayTag, out message);

        // Read the role at impact, never apply mutable bonuses to native stats.
        // Pending revival and stale connections cannot inherit the old carrier.
        internal static bool IsActiveChaser(PlayerAvatar player) => IsPlayTag && phase == Phase.Active && CurrentScope &&
            !stopping && tagBomb != null && !tagBomb.Resolved && !tagBomb.AwaitingRespawn && !tagBomb.IsDue(Now) &&
            player && !player.IsDead && ReferenceEquals(tagPlayers[tagBomb.Carrier].Subject.Player, player) && PlayTagEnrolled(player);

        private static bool CapturePlayTag(out string message)
        {
            ClearPlayTag(false);
            foreach (var spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.PlayerAvatar || !FriendlyFireParticipants.Includes(spawner.PlayerAvatar) ||
                    tagRoster.ContainsKey(spawner.PlayerAvatar)) continue;
                var player = spawner.PlayerAvatar;
                tagRoster.Add(player, tagPlayers.Count);
                tagPlayers.Add(new RosterEntry { Subject = new HostPlayer(spawner), Receipt = FriendlyFireParticipants.Capture(player) });
            }
            message = L.T("Play Tag requires at least two eligible players.");
            if (tagPlayers.Count >= 2)
            { tagRound = 1; tagRoundLimit = SessionSettings.PlayTagRounds; return true; }
            ClearPlayTag(false); return false;
        }

        private static bool PlayTagEnrolled(PlayerAvatar player) => player && tagRoster.TryGetValue(player, out int index) &&
            Connected(tagPlayers[index].Subject, tagPlayers[index].Receipt);

        private static bool PlayTagAllowsPair(PlayerAvatar attacker, PlayerAvatar victim) => phase == Phase.Active && CurrentScope &&
            tagBomb != null && !tagBomb.Resolved && !tagBomb.IsDue(Now) && !ReferenceEquals(attacker, victim) &&
            PlayTagEnrolled(attacker) && PlayTagEnrolled(victim);

        private static void BeginPlayTag(double now)
        {
            // Reservoir selection chooses uniformly from current living entrants.
            int chosen = -1, alive = 0;
            for (int i = 0; i < tagPlayers.Count; i++)
            {
                var entry = tagPlayers[i];
                if (Connected(entry.Subject, entry.Receipt) && entry.Subject.IsReady && !entry.Subject.Player.IsDead &&
                    tagRandom.Next(++alive) == 0) chosen = i;
            }
            if (chosen < 0) { Stop(true, true); return; }
            tagBomb = new PlayTagBomb(tagPlayers.Count, chosen, duration, now);
            ChaserRevision++;
            Broadcast(null, tagRoundLimit == 1 ? L.T("Play Tag start!") : L.F("[Play Tag] Round {0}/{1} start!", tagRound, tagRoundLimit));
            ShowPlayTagCarrier(now);
        }

        private static void AdvancePlayTag(double now)
        {
            if (!IsPlayTag || !CurrentScope || deathDepth != 0 || ReviveAllAction.IsExecuting) return;
            int connected = 0;
            bool ready = true;
            for (int i = 0; i < tagPlayers.Count; i++)
            {
                var entry = tagPlayers[i];
                if (!Connected(entry.Subject, entry.Receipt)) continue;
                connected++;
                if (tagBomb == null && tagRound > 1 && (!entry.Subject.IsReady || entry.Subject.Player.IsDead)) ready = false;
            }
            if (connected < 2 || tagBomb != null && !PlayTagEnrolled(tagPlayers[tagBomb.Carrier].Subject.Player))
            { Stop(true, true); return; }
            if (tagBomb == null) { AdvancePlayTagRound(now, ready); return; }
            if (tagBomb.IsDue(now)) { ExplodePlayTag(now); return; }
            ShowPlayTagCarrier(now);
        }

        // Called only after native normal completion AND successful KDA completion.
        // The captured token/receipts prevent another run or connection inheriting
        // a kill. A pass deliberately leaves combat/assist epochs unchanged.
        internal static void ConfirmedKill(FriendlyFireParticipants.Receipt killer, FriendlyFireParticipants.Receipt victim, long match)
        {
            if (!IsPlayTag || phase != Phase.Active || !CurrentScope || token != match || tagBomb == null ||
                !FriendlyFireParticipants.IsCurrent(killer) || !FriendlyFireParticipants.IsCurrent(victim) || !victim.Player.IsDead ||
                !tagRoster.TryGetValue(killer.Player, out int from) || !tagRoster.TryGetValue(victim.Player, out int to) ||
                !ReferenceEquals(tagPlayers[from].Receipt, killer) || !ReferenceEquals(tagPlayers[to].Receipt, victim) ||
                !tagBomb.TryPass(from, to, Now)) return;
            ChaserRevision++;
            tagShown = -1;
            tagPreviousName = CombatNames.Player(killer.Player);
            Broadcast(killer.Player, L.T("Bomb passed!"));
            if (tagBomb.AwaitingRespawn) Broadcast(victim.Player, L.T("You have the bomb. Fuse begins after respawn."));
            // Immediate third receipt is consumed by LeaveDeath after RpcDie and
            // all nested native death scopes complete, before any revival.
        }

        internal static void Revived(FriendlyFireParticipants.Receipt receipt, long match)
        {
            if (tagBomb == null || !CurrentTagRevival(receipt, match, out int index) || receipt.Player.IsDead ||
                !tagBomb.ArmAfterRespawn(index, Now)) return;
            tagShown = -1;
        }

        internal static void ReviveFailed(FriendlyFireParticipants.Receipt receipt, long match)
        {
            // Native Revive clears IsDead before callbacks and its RPC. An
            // exceptional manual revive cannot arm the fuse or be retried as a
            // completed recovery; end ownership instead of leaving it unarmed.
            if (CurrentTagRevival(receipt, match, out int index) &&
                (tagRound > 1 && phase != Phase.Active || tagBomb != null && tagBomb.AwaitingRespawn && tagBomb.Carrier == index)) Stop(true, true);
        }

        private static bool CurrentTagRevival(FriendlyFireParticipants.Receipt receipt, long match, out int index)
        {
            index = -1;
            return IsPlayTag && CurrentScope && token == match &&
                FriendlyFireParticipants.IsCurrent(receipt) && tagRoster.TryGetValue(receipt.Player, out index) &&
                ReferenceEquals(tagPlayers[index].Receipt, receipt) && Connected(tagPlayers[index].Subject, receipt);
        }

        private static void ShowPlayTagCarrier(double now)
        {
            if (tagBomb == null || tagBomb.Resolved || tagBomb.AwaitingRespawn) return;
            var player = tagPlayers[tagBomb.Carrier].Subject.Player;
            if (!player || player.IsDead) { tagShown = -1; return; }
            int seconds = tagBomb.Remaining(now);
            if (seconds == tagShown) return;
            tagShown = seconds;
            Broadcast(player, L.F("Bomb: {0}s. Pass the bomb by defeating another player!", seconds));
        }

        internal static bool IsPlayTagExplosion(PlayerAvatar player) => ReferenceEquals(player, tagExplosionVictim);

        private static void ExplodePlayTag(double now)
        {
            if (tagBomb == null || !tagBomb.ConsumeExplosion(now)) return;
            long current = token;
            var victim = tagPlayers[tagBomb.Carrier].Subject.Player;
            string message = tagBomb.PreviousCarrier < 0 ? L.F("Play Tag: {0}'s bomb exploded before any pass.", CombatNames.Player(victim)) :
                L.F("'{0}' was blown up by '{1}'s bomb.", CombatNames.Player(victim), tagPreviousName);
            Broadcast(null, message);
            try { PlayTagExplosion.Show(victim); }
            catch (Exception error) { UnityEngine.Debug.LogWarning("[SephiriaOne] Play Tag explosion presentation failed: " + error); }
            var previous = tagExplosionVictim;
            tagExplosionVictim = victim;
            bool failed = false;
            try
            {
                // Keep match enrollment active for the existing Die hook's
                // all-dead protection; never repeat an already completed death.
                if (victim && !victim.IsDead && IsPlayTag && current == token && CurrentScope) victim.Die(5, null);
            }
            catch (Exception error) { failed = true; UnityEngine.Debug.LogWarning("[SephiriaOne] Play Tag bomb death failed: " + error); }
            finally
            {
                tagExplosionVictim = previous;
                if (IsPlayTag && current == token)
                {
                    if (!failed && CurrentScope && tagRound < tagRoundLimit) CompletePlayTagRound();
                    else
                    {
                        tagNaturalStop = true;
                        try { Stop(true, true); }
                        finally { tagNaturalStop = false; }
                    }
                }
            }
        }

        private static void ClearPlayTag(bool preserveExplosion)
        {
            // Replacement clears both live carrier instructions and dead-player
            // countdowns through the stock avatar-chat path.
            foreach (var entry in tagPlayers)
                if (Connected(entry.Subject, entry.Receipt)) Broadcast(entry.Subject.Player, "");
            tagBomb = null; tagPlayers.Clear(); tagRoster.Clear(); tagShown = -1; tagPreviousName = null;
            tagRound = tagRoundLimit = 0;
            ChaserRevision++;
            if (!preserveExplosion) PlayTagExplosion.Reset();
        }

        internal static string DescribePlayTag() => L.F("Play Tag: {0}. Initial fuse: {1}s. Remaining: {2}s.",
            L.T(!IsPlayTag ? "off" : phase == Phase.Countdown ? "countdown" : phase == Phase.BetweenRounds || tagBomb != null && tagBomb.AwaitingRespawn ? "waiting" : "active"),
            IsPlayTag ? duration : SessionSettings.PlayTagDuration,
            !IsPlayTag ? 0 : phase == Phase.Countdown ? Math.Max(0, (int)Math.Ceiling(startsAt - Now)) : tagBomb?.Remaining(Now) ?? 0) + " " + DescribePlayTagRound();
    }
}
