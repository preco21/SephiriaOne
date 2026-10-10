using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace SephiriaOne
{
    // One host-owned, temporary match. No coroutines or per-player controllers.
    // Every callback/timer revalidates the captured run and avatar identities.
    internal static class DeathmatchRuntime
    {
        private enum Phase { Idle, Countdown, Active }
        private sealed class Respawn
        {
            internal HostPlayer Subject;
            internal double At;
            internal int Shown;
        }
        private sealed class Participant
        {
            internal FriendlyFireKda.Score Score;
            internal string Name;
            internal int Order;
        }
        private static Phase phase;
        private static DungeonManager dungeon;
        private static SaveData run;
        private static uint dungeonId;
        private static long generation, token;
        private static double startsAt, endsAt, nextScan;
        private static int openingShown, duration;
        private static bool stopping, ticking;
        private static List<Respawn> cleanup;
        private static int deathDepth;
        private static readonly HashSet<PlayerAvatar> dying = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<PlayerAvatar, Respawn> pending = new Dictionary<PlayerAvatar, Respawn>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<PlayerAvatar, HostPlayer> roster = new Dictionary<PlayerAvatar, HostPlayer>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly Dictionary<FriendlyFireKda.Score, Participant> participants = new Dictionary<FriendlyFireKda.Score, Participant>();
        private static readonly HashSet<PlayerAvatar> seen = new HashSet<PlayerAvatar>(ReferenceComparer<PlayerAvatar>.Instance);
        private static readonly List<PlayerAvatar> departed = new List<PlayerAvatar>();
        private static readonly List<Respawn> due = new List<Respawn>();
        internal static bool IsRunning => phase != Phase.Idle;
        internal static bool RecoveryPending => stopping;
        private static double Now => Time.unscaledTime;
        private static bool CurrentScope => NetworkServer.active && NetworkClient.active && dungeon && dungeon.isServer && dungeon.netId == dungeonId &&
            ReferenceEquals(dungeon, DungeonManager.Instance) && ReferenceEquals(run, SaveManager.CurrentRun) &&
            generation == SessionSettings.ResourceGeneration && ReviveAllAction.IsRunOpen(dungeon, run);
        internal static bool CanStart => !IsRunning && !stopping && !ticking && deathDepth == 0 && DeathmatchFeature.Available && FriendlyFireFeature.Available && ReviveAllAction.CanExecute;
        internal static bool CanResetScores => IsRunning && !stopping && CurrentScope && Now < endsAt;
        internal static FriendlyFireSettings Effective(FriendlyFireSettings normal) => !IsRunning ? normal :
            normal.WithEnabled(phase == Phase.Active && CurrentScope && Now < endsAt);

        internal static bool ResetScores(out string message)
        {
            message = L.T("K/D/A reset requires a current deathmatch countdown or active match.");
            if (!CanResetScores) return false;
            FriendlyFireKda.ResetScores();
            // A departed offline participant can outlive its weak identity key.
            // Keep that entry and its tie-break order, but clear its totals too.
            foreach (var participant in participants.Values) participant.Score.Clear();
            message = L.T("Deathmatch K/D/A scores reset.");
            Broadcast(null, message);
            return true;
        }

        internal static bool Start(out string message)
        {
            message = L.T("Deathmatch is unavailable, already running, or the session is ending. Check /one deathmatch status and Player.log.");
            if (!NetworkServer.active || !NetworkClient.active || !SessionSettings.EnsureResourceScope() || !CanStart ||
                !HostStateAdapter.TryCollect(out var context, out message)) return false;
            // Capture before entering callbacks. A fresh match never reuses a timer,
            // score, or connection from an earlier match.
            dungeon = context.Dungeon; dungeonId = dungeon.netId; run = SaveManager.CurrentRun;
            generation = SessionSettings.ResourceGeneration; token++;
            duration = SessionSettings.DeathmatchDuration; startsAt = Now + 3; endsAt = startsAt + duration;
            openingShown = 3; nextScan = 0; phase = Phase.Countdown;
            pending.Clear(); roster.Clear(); participants.Clear(); FriendlyFireKda.SetEnabled(false);
            SessionSettings.DeathmatchChanged();
            Broadcast(null, L.T("3..."));
            ObserveRoster();
            message = L.F("Deathmatch starts in 3 seconds and lasts {0} seconds.", duration);
            return true;
        }

        internal static void Tick()
        {
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
                if (now >= endsAt) { Stop(true, true); return; }
                if (phase == Phase.Countdown)
                {
                    int seconds = (int)Math.Ceiling(startsAt - now);
                    if (seconds > 0)
                    { if (seconds != openingShown) { openingShown = seconds; Broadcast(null, L.F("{0}...", seconds)); } }
                    else
                    {
                        phase = Phase.Active; FriendlyFireKda.Reset(); FriendlyFireKda.SetEnabled(true); participants.Clear();
                        SessionSettings.DeathmatchChanged(); Broadcast(null, L.T("Deathmatch start!"));
                    }
                }
                if (now < nextScan) return;
                nextScan = now + 0.1;
                long current = token;
                ObserveRoster();
                due.Clear(); foreach (var respawn in pending.Values) due.Add(respawn);
                for (int i = 0; i < due.Count && current == token && IsRunning; i++)
                {
                    var respawn = due[i]; var player = respawn.Subject.Player;
                    if (!pending.TryGetValue(player, out var latest) || !ReferenceEquals(latest, respawn)) continue;
                    if (!Connected(respawn.Subject) || !player.IsDead) { pending.Remove(player); continue; }
                    if (!respawn.Subject.IsReady) continue;
                    int seconds = Math.Max(0, (int)Math.Ceiling(respawn.At - now));
                    if (seconds > 0)
                    {
                        if (respawn.Shown != seconds) { respawn.Shown = seconds; Broadcast(player, L.F("Respawn in: {0}s", seconds)); }
                        continue;
                    }
                    pending.Remove(player);
                    if (!Restore(respawn, current, out string failure))
                    {
                        Broadcast(null, failure);
                        Stop(true, true); break;
                    }
                    Broadcast(player, L.T("Respawned!"));
                }
            }
            catch (Exception error)
            { Debug.LogWarning("[SephiriaOne] Deathmatch stopped after an error: " + error); Stop(true, true); }
            finally { due.Clear(); ticking = false; if (stopping) DrainRecovery(); }
        }

        private static bool Connected(HostPlayer subject) => subject.Spawner && subject.Player && subject.Player.netId == subject.Id &&
            ReferenceEquals(subject.Spawner.PlayerAvatar, subject.Player) && ReferenceEquals(subject.Player.spawner, subject.Spawner) &&
            PlayerSpawner.MultiplayerList.Contains(subject.Spawner);

        private static bool Restore(Respawn respawn, long current, out string failure) =>
            ReviveAllAction.TryRevive(respawn.Subject, () => current == token && CurrentScope, out failure);

        private static void ObserveRoster()
        {
            seen.Clear(); departed.Clear();
            foreach (PlayerSpawner spawner in PlayerSpawner.MultiplayerList)
            {
                if (!spawner || !spawner.isServer || !spawner.PlayerAvatar || !seen.Add(spawner.PlayerAvatar)) continue;
                var player = spawner.PlayerAvatar;
                if (!roster.TryGetValue(player, out var subject) || !Connected(subject))
                    roster[player] = subject = new HostPlayer(spawner);
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
            if (!IsRunning || !CurrentScope || !player || !player.IsDead || pending.ContainsKey(player)) return;
            var spawner = player.spawner;
            if (!spawner || !ReferenceEquals(spawner.PlayerAvatar, player) || !PlayerSpawner.MultiplayerList.Contains(spawner)) return;
            var subject = new HostPlayer(spawner);
            pending.Add(player, new Respawn { Subject = subject, At = Now + 3, Shown = 3 });
            var score = FriendlyFireKda.For(player);
            if (!participants.TryGetValue(score, out var entry)) participants.Add(score, entry = new Participant { Score = score, Order = participants.Count });
            entry.Name = CombatNames.Player(player);
            Broadcast(player, L.F("Respawn in: {0}s", 3));
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
                !PlayerSpawner.MultiplayerList.Contains(player.spawner) || !dying.Add(player)) return false;
            deathDepth++; return true;
        }
        internal static void LeaveDeath(PlayerAvatar player, bool entered)
        {
            if (!entered) return;
            dying.Remove(player); deathDepth--;
            if (stopping && cleanup != null && CurrentScope && player && player.IsDead && player.spawner &&
                ReferenceEquals(player.spawner.PlayerAvatar, player) && PlayerSpawner.MultiplayerList.Contains(player.spawner) &&
                !cleanup.Exists(r => ReferenceEquals(r.Subject.Player, player)))
                cleanup.Add(new Respawn { Subject = new HostPlayer(player.spawner) });
            if (stopping) DrainRecovery();
        }
        internal static bool ProtectDeath(PlayerAvatar player) => (IsRunning || stopping && dying.Contains(player)) && CurrentScope && ReviveAllFeature.Available &&
            player && player.IsDead && player.spawner && ReferenceEquals(player.spawner.PlayerAvatar, player) && PlayerSpawner.MultiplayerList.Contains(player.spawner);

        // Called by manual toggles even when their value is unchanged. Timer
        // ownership ends before any revival can invoke user/game callbacks.
        internal static void Stop(bool recover, bool announce)
        {
            if (stopping)
            {
                if (!recover) { token++; FinishStop(); }
                return;
            }
            if (!IsRunning) return;
            stopping = true;
            bool same = CurrentScope;
            cleanup = recover && same ? new List<Respawn>(pending.Values) : null;
            if (cleanup != null)
                foreach (var spawner in PlayerSpawner.MultiplayerList)
                    if (spawner && spawner.isServer && spawner.PlayerAvatar && spawner.PlayerAvatar.IsDead &&
                        !cleanup.Exists(r => ReferenceEquals(r.Subject.Player, spawner.PlayerAvatar)))
                        cleanup.Add(new Respawn { Subject = new HostPlayer(spawner) });
            foreach (var subject in roster.Values)
                if (Connected(subject) && participants.TryGetValue(FriendlyFireKda.For(subject.Player), out var participant)) participant.Name = CombatNames.Player(subject.Player);
            var results = new List<Participant>(participants.Values);
            results.Sort((a, b) => { int c = b.Score.Kills.CompareTo(a.Score.Kills); if (c == 0) c = a.Score.Deaths.CompareTo(b.Score.Deaths);
                if (c == 0) c = b.Score.Assists.CompareTo(a.Score.Assists); return c != 0 ? c : a.Order.CompareTo(b.Order); });
            phase = Phase.Idle; pending.Clear(); roster.Clear(); participants.Clear();
            try
            {
                SessionSettings.EndDeathmatch();
                if (announce && same && CurrentScope)
                {
                    Broadcast(null, L.T("Deathmatch ended. Top 5 (K/D/A):"));
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
                    if (Connected(respawn.Subject) && respawn.Subject.Player.IsDead)
                    {
                        if (!ReviveAllAction.TryRevive(respawn.Subject, () => current == token && CurrentScope, out string failure)) Broadcast(null, failure);
                        else Broadcast(respawn.Subject.Player, L.T("Respawned!"));
                    }
                }
            }
            finally { FinishStop(); }
        }
        private static void FinishStop()
        { cleanup = null; dungeon = null; run = null; stopping = false; SessionSettings.DeathmatchChanged(); }

        private static void Broadcast(PlayerAvatar player, string message)
        {
            if (!CurrentScope) return;
            try { dungeon.Chat(player, "SephiriaOne", message); }
            catch (Exception error) { Debug.LogWarning("[SephiriaOne] Deathmatch notice failed: " + error); }
        }
        internal static string Describe() => L.F("Deathmatch: {0}. Configured duration: {1}s. Remaining: {2}s.",
            L.T(phase == Phase.Countdown ? "countdown" : phase == Phase.Active ? "active" : "off"), SessionSettings.DeathmatchDuration,
            IsRunning ? Math.Max(0, (int)Math.Ceiling((phase == Phase.Countdown ? startsAt : endsAt) - Now)) : 0);
    }
}
