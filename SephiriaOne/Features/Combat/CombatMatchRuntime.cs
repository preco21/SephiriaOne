using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // One host-owned, temporary match. No coroutines or per-player controllers.
    // Every callback/timer revalidates the captured run and avatar identities.
    internal static partial class CombatMatchRuntime
    {
        private enum Phase { Idle, Countdown, Active, BetweenRounds }
        private enum MatchMode { None, Deathmatch, Tournament, PlayTag, ZombieSurvival }
        private sealed class Respawn
        {
            internal HostPlayer Subject;
            internal FriendlyFireParticipants.Receipt Receipt;
            internal double At;
            internal bool ZombieConversionNoticePending;
            internal string ZombieInfector;
        }
        private sealed class RosterEntry
        {
            internal HostPlayer Subject;
            internal FriendlyFireParticipants.Receipt Receipt;
            internal ZombieMovementSpeed Movement;
        }
        private sealed class Participant
        {
            internal FriendlyFireKda.Score Score;
            internal string Name;
            internal int Order;
        }
        private static Phase phase;
        private static MatchMode mode;
        private static DungeonManager dungeon;
        private static SaveData run;
        private static uint dungeonId;
        private static long generation, token;
        private static double startsAt, endsAt, nextScan;
        private static int openingShown, duration;
        private static bool stopping, ticking;
        private static List<Respawn> cleanup;
        private static int deathDepth;
        private static readonly Dictionary<PlayerAvatar, FriendlyFireParticipants.Receipt> dying = new Dictionary<PlayerAvatar, FriendlyFireParticipants.Receipt>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<PlayerAvatar, Respawn> pending = new Dictionary<PlayerAvatar, Respawn>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<PlayerAvatar, RosterEntry> roster = new Dictionary<PlayerAvatar, RosterEntry>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<FriendlyFireKda.Score, Participant> participants = new Dictionary<FriendlyFireKda.Score, Participant>();
        private static readonly HashSet<PlayerAvatar> seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly List<PlayerAvatar> departed = new List<PlayerAvatar>();
        private static readonly List<Respawn> due = new List<Respawn>();
        internal static bool IsRunning => phase != Phase.Idle;
        internal static bool IsDeathmatch => mode == MatchMode.Deathmatch && IsRunning;
        internal static long MatchToken => IsRunning ? token : 0;
        internal static bool RecoveryPending => stopping;
        private static double Now => Time.unscaledTime;
        private static bool CurrentScope => NetworkServer.active && NetworkClient.active && dungeon && dungeon.isServer && dungeon.netId == dungeonId &&
            ReferenceEquals(dungeon, DungeonManager.Instance) && ReferenceEquals(run, SaveManager.CurrentRun) &&
            generation == SessionSettings.ResourceGeneration && ReviveAllAction.IsRunOpen(dungeon, run);
        internal static bool CanStart => !IsRunning && !stopping && !ticking && deathDepth == 0 && DeathmatchFeature.Available && FriendlyFireFeature.Available && ReviveAllAction.CanExecute;
        internal static bool CanResetScores => IsRunning && !stopping && CurrentScope &&
            (mode == MatchMode.Tournament || mode == MatchMode.PlayTag && (tagBomb == null || !tagBomb.IsDue(Now) && !tagBomb.Resolved) || (IsDeathmatch || IsZombieSurvival) && Now < endsAt);
        internal static FriendlyFireSettings Effective(FriendlyFireSettings normal) => !IsRunning ? normal :
            normal.WithEnabled(phase == Phase.Active && CurrentScope &&
                (mode == MatchMode.PlayTag ? tagBomb != null && !tagBomb.Resolved && !tagBomb.IsDue(Now) : Now < endsAt));
        // Round countdowns block damage without destroying accumulated scores.
        internal static bool ScoresEnabled(bool normal) => mode != MatchMode.Deathmatch && IsRunning && CurrentScope || normal;

        internal static bool ResetScores(out string message)
        {
            message = L.T("K/D/A reset requires a current combat match.");
            if (!CanResetScores) return false;
            FriendlyFireKda.ResetScores();
            // A departed offline participant can outlive its weak identity key.
            // Keep that entry and its tie-break order, but clear its totals too.
            foreach (var participant in participants.Values) participant.Score.Clear();
            message = IsZombieSurvival ? L.T("Zombie Survival K/D/A scores reset.") : IsPlayTag ? L.T("Play Tag K/D/A scores reset.") : IsTournament ? L.T("Tournament K/D/A scores reset.") : L.T("Deathmatch K/D/A scores reset.");
            Broadcast(null, message);
            return true;
        }

        internal static bool Start(out string message) => Start(MatchMode.Deathmatch, out message);

        private static bool Start(MatchMode requested, out string message)
        {
            bool tournament = requested == MatchMode.Tournament, tag = requested == MatchMode.PlayTag, zombie = requested == MatchMode.ZombieSurvival;
            message = zombie ? L.T("Zombie Survival is unavailable, already running, or the session is ending. Check /one zombie status and Player.log.") : tag ? L.T("Play Tag is unavailable, already running, or the session is ending. Check /one tag status and Player.log.") :
                tournament ? L.T("Tournament is unavailable, already running, or the session is ending. Check /one tournament status and Player.log.") :
                L.T("Deathmatch is unavailable, already running, or the session is ending. Check /one deathmatch status and Player.log.");
            if (!NetworkServer.active || !NetworkClient.active || !SessionSettings.EnsureResourceScope() || !CanStart ||
                !HostStateAdapter.TryCollect(out var context, out message)) return false;
            if (tournament && !CaptureTournament(out message)) return false;
            if (tag && !CapturePlayTag(out message)) return false;
            if (zombie && !CaptureZombie(out message)) return false;
            PlayTagExplosion.Reset();
            // Capture before entering callbacks. A fresh match never reuses a timer,
            // score, or connection from an earlier match.
            dungeon = context.Dungeon; dungeonId = dungeon.netId; run = SaveManager.CurrentRun;
            generation = SessionSettings.ResourceGeneration; token++;
            mode = requested;
            duration = zombie ? SessionSettings.ZombieDuration : tag ? SessionSettings.PlayTagDuration : tournament ? SessionSettings.TournamentDuration : SessionSettings.DeathmatchDuration;
            startsAt = Now + 3; endsAt = startsAt + duration;
            openingShown = 3; nextScan = 0; phase = Phase.Countdown;
            pending.Clear(); roster.Clear(); participants.Clear(); FriendlyFireKda.SetEnabled(false);
            if (tournament) { FriendlyFireKda.Reset(); FriendlyFireKda.SetEnabled(true); phase = Phase.BetweenRounds; }
            SessionSettings.DeathmatchChanged();
            if (zombie) ShowZombieCountdown(3);
            else if (tag) ShowPlayTagCountdown(3);
            else if (!tournament) Broadcast(null, L.T("3..."));
            ObserveRoster();
            if (tournament) AdvanceTournament(Now);
            message = zombie ? L.F("Zombie Survival starts in 3 seconds with {0} players; round: {1}s.", zombiePlayers.Count, duration) : tag ? L.F("Play Tag starts in 3 seconds with {0} players; initial fuse: {1}s.", tagPlayers.Count, duration) :
                tournament ? L.F("Tournament started with {0} players; each round lasts {1} seconds.", tournamentPlayers.Count, duration) :
                L.F("Deathmatch starts in 3 seconds and lasts {0} seconds.", duration);
            return true;
        }

        internal static void Tick()
        {
            PlayTagExplosion.Tick();
            if (stopping) { DrainRecovery(); return; }
            if (!IsRunning || ticking || stopping) return;
            ticking = true;
            try
            {
                if (!CurrentScope) { Stop(false, false); return; }
                if (!DeathmatchFeature.Available || !FriendlyFireFeature.Available || !ReviveAllFeature.Available)
                { Stop(true, true); return; }
                double now = Now;
                // Expiry wins over a respawn due on the same frame.
                if (IsDeathmatch && now >= endsAt) { Stop(true, true); return; }
                if (IsTournament) AdvanceTournament(now);
                if (IsZombieSurvival) AdvanceZombie(now);
                if (IsPlayTag) { AdvancePlayTag(now); if (deathDepth != 0) return; }
                if (!IsRunning) return;
                if (!IsTournament && phase == Phase.Countdown)
                {
                    int seconds = (int)Math.Ceiling(startsAt - now);
                    if (seconds > 0)
                    {
                        if (seconds != openingShown)
                        {
                            openingShown = seconds;
                            if (IsZombieSurvival) ShowZombieCountdown(seconds); else if (IsPlayTag) ShowPlayTagCountdown(seconds); else Broadcast(null, L.F("{0}...", seconds));
                        }
                    }
                    else
                    {
                        phase = Phase.Active;
                        if (!IsPlayTag || tagRound == 1)
                        { FriendlyFireKda.Reset(); FriendlyFireKda.SetEnabled(true); participants.Clear(); }
                        SessionSettings.DeathmatchChanged();
                        if (IsZombieSurvival) BeginZombie(now); else if (IsPlayTag) BeginPlayTag(now); else Broadcast(null, L.T("Deathmatch start!"));
                    }
                }
                if (!IsRunning) return;
                if (now < nextScan) return;
                nextScan = now + 0.1;
                long current = token;
                ObserveRoster();
                due.Clear(); foreach (var respawn in pending.Values) due.Add(respawn);
                for (int i = 0; i < due.Count && current == token && IsRunning; i++)
                {
                    var respawn = due[i]; var player = respawn.Subject.Player;
                    if (!pending.TryGetValue(player, out var latest) || !ReferenceEquals(latest, respawn)) continue;
                    if (IsZombieSurvival && dying.ContainsKey(player)) continue; // Wait for infection attribution/finalization.
                    if (!Connected(respawn.Subject, respawn.Receipt) || !Eligible(player) || !player.IsDead) { pending.Remove(player); continue; }
                    if (!respawn.Subject.IsReady) continue;
                    if (now < respawn.At) continue;
                    pending.Remove(player);
                    if (!Restore(respawn, current, out string failure))
                    {
                        Broadcast(null, failure);
                        Stop(true, true); break;
                    }
                }
                if (IsTournament && current == token) AdvanceTournament(now);
                if (IsPlayTag && current == token) AdvancePlayTag(now);
                if (IsZombieSurvival && current == token) AdvanceZombie(now);
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Combat match stopped after an error: " + error); Stop(true, true); }
            finally { due.Clear(); ticking = false; if (stopping) DrainRecovery(); }
        }

        private static bool Connected(HostPlayer subject, FriendlyFireParticipants.Receipt receipt) => FriendlyFireParticipants.IsCurrent(receipt) &&
            subject.Spawner && subject.Player && subject.Player.netId == subject.Id &&
            ReferenceEquals(subject.Spawner.PlayerAvatar, subject.Player) && ReferenceEquals(subject.Player.spawner, subject.Spawner) &&
            PlayerSpawner.MultiplayerList.Contains(subject.Spawner);

        // Loading temporarily blocks revival, not participation or its deadline.
        // Capture still rejects retired/replaced connections even while loading.
        // Supplies cover the full captured roster, including countdowns and waiting duelists.
        internal static bool IsSupplyParticipant(PlayerAvatar player) => IsRunning && CurrentScope && Eligible(player);

        private static bool Eligible(PlayerAvatar player) => FriendlyFireParticipants.Includes(player, requireReady: false) &&
            (mode == MatchMode.ZombieSurvival ? ZombieEnrolled(player) : mode == MatchMode.Tournament ? TournamentEnrolled(player) : mode != MatchMode.PlayTag || PlayTagEnrolled(player));

        private static bool Restore(Respawn respawn, long current, out string failure)
        {
            if (!ReviveAllAction.TryRevive(respawn.Subject, () => current == token && CurrentScope &&
                Connected(respawn.Subject, respawn.Receipt), out failure, out int restored)) return false;
            // Only the completed countdown uses this path. The native revive
            // RPC has finished; recheck callbacks before emitting presentation.
            var player = respawn.Subject.Player;
            // Eligibility may prune stale selections and stop Tournament.
            // Validate match identity after all predicates that can change it.
            if (restored == 1 && Eligible(player) && Connected(respawn.Subject, respawn.Receipt) &&
                IsRunning && current == token && CurrentScope && !player.IsDead)
            {
                try { NativeReviveVisuals.Show(player); }
                catch (Exception error) { Debug.LogWarning("[SephiriaOne] Respawn FX failed after successful recovery: " + error); }
            }
            return true;
        }

        private static void ObserveRoster()
        {
            seen.Clear(); departed.Clear();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!IsRunning) return;
                if (!spawner || !spawner.isServer || !spawner.PlayerAvatar || !Eligible(spawner.PlayerAvatar) ||
                    !seen.Add(spawner.PlayerAvatar)) continue;
                if (!IsRunning) return;
                var player = spawner.PlayerAvatar;
                if (!roster.TryGetValue(player, out var entry) || !Connected(entry.Subject, entry.Receipt))
                {
                    if (pending.TryGetValue(player, out var prior) && !Connected(prior.Subject, prior.Receipt)) pending.Remove(player);
                    roster[player] = entry = new RosterEntry { Subject = new HostPlayer(spawner), Receipt = FriendlyFireParticipants.Capture(player) };
                }
                var subject = entry.Subject;
                if (!subject.IsReady) continue;
                var score = FriendlyFireKda.For(player);
                if (!participants.ContainsKey(score)) participants.Add(score, new Participant { Score = score, Name = CombatNames.Player(player), Order = participants.Count });
                if (player.IsDead) Died(player);
                else pending.Remove(player);
            }
            foreach (var player in roster.Keys) if (!seen.Contains(player)) departed.Add(player);
            foreach (var player in departed) { roster.Remove(player); pending.Remove(player); }
        }

        internal static void Died(PlayerAvatar player)
        {
            if (!IsRunning || !CurrentScope || !player || !player.IsDead || !Eligible(player)) return;
            if (!IsRunning) return; // Selection pruning may have stopped the match.
            if (IsTournament)
            {
                long current = token;
                var receipt = FriendlyFireParticipants.Capture(player);
                TournamentDeath(player);
                // Native chat callbacks may stop, revive or replace the fighter.
                if (!IsRunning || current != token || !CurrentScope || !player || !player.IsDead ||
                    !FriendlyFireParticipants.IsCurrent(receipt)) return;
            }
            if (pending.TryGetValue(player, out var prior))
            {
                if (Connected(prior.Subject, prior.Receipt)) return;
                pending.Remove(player);
            }
            var spawner = player.spawner;
            if (!spawner || !ReferenceEquals(spawner.PlayerAvatar, player) || !PlayerSpawner.MultiplayerList.Contains(spawner)) return;
            var subject = new HostPlayer(spawner);
            int delay = IsPlayTag || IsZombieSurvival ? 1 : 3;
            pending.Add(player, new Respawn { Subject = subject, Receipt = FriendlyFireParticipants.Capture(player), At = Now + delay });
            var score = FriendlyFireKda.For(player);
            if (!participants.TryGetValue(score, out var entry)) participants.Add(score, entry = new Participant { Score = score, Order = participants.Count });
            entry.Name = CombatNames.Player(player);
        }
        internal static void Reviving(PlayerAvatar player)
        {
            if (ReferenceEquals(player, null)) return;
            pending.Remove(player);
            cleanup?.RemoveAll(r => ReferenceEquals(r.Subject.Player, player));
        }
        internal static bool EnterDeath(PlayerAvatar player)
        {
            if (!IsRunning || !CurrentScope || !player || !player.spawner || !ReferenceEquals(player.spawner.PlayerAvatar, player) ||
                !PlayerSpawner.MultiplayerList.Contains(player.spawner) || !Eligible(player) || dying.ContainsKey(player)) return false;
            if (!IsRunning) return false;
            dying.Add(player, FriendlyFireParticipants.Capture(player));
            deathDepth++; return true;
        }
        internal static void LeaveDeath(PlayerAvatar player, bool entered)
        {
            if (!entered) return;
            dying.TryGetValue(player, out var receipt); dying.Remove(player); deathDepth--;
            if (stopping && cleanup != null && CurrentScope && FriendlyFireParticipants.IsCurrent(receipt) && player && player.IsDead && player.spawner &&
                ReferenceEquals(player.spawner.PlayerAvatar, player) && PlayerSpawner.MultiplayerList.Contains(player.spawner) &&
                !cleanup.Exists(r => ReferenceEquals(r.Subject.Player, player)))
                cleanup.Add(new Respawn { Subject = new HostPlayer(player.spawner), Receipt = receipt });
            if (stopping) DrainRecovery();
            else if (IsZombieSurvival && CurrentScope && player && player.IsDead &&
                pending.TryGetValue(player, out var respawn) && respawn.ZombieConversionNoticePending && Connected(respawn.Subject, respawn.Receipt))
            {
                // Keep the one-time faction alert, without any respawn text.
                // Consume before chat callbacks can reenter or stop the match.
                respawn.ZombieConversionNoticePending = false;
                Broadcast(player, ZombieSurvivalMessages.Conversion(respawn.ZombieInfector));
            }
            else if (IsPlayTag && deathDepth == 0) AdvancePlayTag(Now);
            // Zombie victory is committed by infection but concluded on Tick.
            // A lethal reflected hit may finish this death before the enclosing
            // attack reaches its own victim's Die; recovery must include both.
        }
        internal static bool ProtectDeath(PlayerAvatar player) => player && player.IsDead && CurrentScope && ReviveAllFeature.Available &&
            (dying.TryGetValue(player, out var receipt) && FriendlyFireParticipants.IsCurrent(receipt) && (IsRunning || stopping) ||
                IsRunning && Eligible(player));

        // Called by manual toggles even when their value is unchanged. Timer
        // ownership ends before any revival can invoke user/game callbacks.
        internal static void Stop(bool recover, bool announce)
        {
            if (stopping)
            {
                PlayTagExplosion.Reset();
                if (!recover) { token++; FinishStop(); }
                return;
            }
            if (!IsRunning) { PlayTagExplosion.Reset(); return; }
            stopping = true;
            bool same = CurrentScope;
            cleanup = recover && same ? new List<Respawn>(pending.Values) : null;
            if (cleanup != null)
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                    if (spawner && spawner.isServer && spawner.PlayerAvatar && spawner.PlayerAvatar.IsDead &&
                        Eligible(spawner.PlayerAvatar) &&
                        !cleanup.Exists(r => ReferenceEquals(r.Subject.Player, spawner.PlayerAvatar)))
                        cleanup.Add(new Respawn { Subject = new HostPlayer(spawner), Receipt = FriendlyFireParticipants.Capture(spawner.PlayerAvatar) });
            foreach (var entry in roster.Values)
                if (Connected(entry.Subject, entry.Receipt) && participants.TryGetValue(FriendlyFireKda.For(entry.Subject.Player), out var participant))
                    participant.Name = CombatNames.Player(entry.Subject.Player);
            var results = new List<Participant>(participants.Values);
            results.Sort((a, b) => { int c = b.Score.Kills.CompareTo(a.Score.Kills); if (c == 0) c = a.Score.Deaths.CompareTo(b.Score.Deaths);
                if (c == 0) c = b.Score.Assists.CompareTo(a.Score.Assists); return c != 0 ? c : a.Order.CompareTo(b.Order); });
            MatchMode previousMode = mode;
            var zombieResults = IsZombieSurvival ? ZombieResults() : null;
            phase = Phase.Idle; pending.Clear(); roster.Clear(); participants.Clear();
            ClearTournamentMessages();
            ClearPlayTag(same && tagNaturalStop && tagBomb != null && tagBomb.Resolved);
            ClearZombie();
            try
            {
                SessionSettings.EndDeathmatch();
                if (announce && same && CurrentScope && zombieResults != null)
                    foreach (string line in zombieResults) Broadcast(null, line);
                else if (announce && same && CurrentScope)
                {
                    Broadcast(null, previousMode == MatchMode.PlayTag ? L.T("Play Tag ended. Top 5 (K/D/A):") :
                        previousMode == MatchMode.Tournament ? L.T("Tournament ended. Top 5 (K/D/A):") : L.T("Deathmatch ended. Top 5 (K/D/A):"));
                    for (int i = 0; i < Math.Min(5, results.Count); i++) Broadcast(null, L.F("{0}. {1}", i + 1, results[i].Score.Label(results[i].Name)));
                }
            }
            finally { if (cleanup == null) FinishStop(); else DrainRecovery(); }
        }

        private static void DrainRecovery()
        {
            // A toggle may be clicked from a native revival callback. Let that
            // exact revival finish its RPC, then drain the other players in the
            // same tick (or next frame for a manual revive-all callback).
            if (!stopping || ReviveAllAction.IsExecuting || deathDepth != 0) return;
            long current = token;
            try
            {
                while (cleanup != null && cleanup.Count > 0 && current == token && CurrentScope)
                {
                    var respawn = cleanup[cleanup.Count - 1]; cleanup.RemoveAt(cleanup.Count - 1);
                    if (Connected(respawn.Subject, respawn.Receipt) && respawn.Subject.Player.IsDead)
                    {
                        if (!ReviveAllAction.TryRevive(respawn.Subject, () => current == token && CurrentScope && Connected(respawn.Subject, respawn.Receipt), out string failure, out _)) Broadcast(null, failure);
                    }
                }
            }
            finally { FinishStop(); }
        }
        private static void FinishStop()
        { cleanup = null; dungeon = null; run = null; stopping = false; mode = MatchMode.None; ClearTournament(); ClearZombie(); SessionSettings.DeathmatchChanged(); }

        private static void BroadcastCountdown(PlayerAvatar player, string message, string floating)
        {
            long current = token, chaser = RoleRevision;
            var bomb = tagBomb;
            var receipt = FriendlyFireParticipants.Capture(player);
            Broadcast(player, message); // Retained during the floating-text trial.
            if (!FriendlyFireParticipants.IsCurrent(receipt) || !IsRunning || current != token || !CurrentScope) return;
            // Chat hooks can transfer the bomb without changing the match token.
            // Its countdown must still belong to the same living carrier.
            if (!IsPlayTag || player.IsDead || bomb == null || !ReferenceEquals(bomb, tagBomb) || chaser != RoleRevision ||
                bomb.Resolved || bomb.AwaitingRespawn || bomb.IsDue(Now) || !ReferenceEquals(tagPlayers[bomb.Carrier].Subject.Player, player)) return;
            try { NativePlayerFloatingText.Show(player, floating); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Optional countdown presentation failed: " + error); }
        }

        private static void Broadcast(PlayerAvatar player, string message)
        {
            if (!CurrentScope) return;
            try { dungeon.Chat(player, "SephiriaOne", message); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Combat match notice failed: " + error); }
        }
        internal static string Describe() => L.F("Deathmatch: {0}. Configured duration: {1}s. Remaining: {2}s.",
            L.T(!IsDeathmatch ? "off" : phase == Phase.Countdown ? "countdown" : "active"), SessionSettings.DeathmatchDuration,
            IsDeathmatch ? Math.Max(0, (int)Math.Ceiling((phase == Phase.Countdown ? startsAt : endsAt) - Now)) : 0);
    }
}
