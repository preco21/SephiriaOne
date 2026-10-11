using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Same event-time scope resolution as potions. Never cache per-player
        // policy or rely on the joining connection's prior incarnation.
        internal static FriendlyFireSettings FriendlyFireForHit => EnsureResourceScope() ? CombatMatchRuntime.Effective(policy.FriendlyFire) : default;

        internal static bool CanResetFriendlyFireScores => enabled && NetworkServer.active && !CombatMatchRuntime.RecoveryPending &&
            (CombatMatchRuntime.IsRunning ? CombatMatchRuntime.CanResetScores :
                dungeon && dungeon.isServer && dungeon.netId != 0 && ReferenceEquals(dungeon, DungeonManager.Instance) &&
                ReviveAllAction.IsRunOpen(dungeon, SaveManager.CurrentRun));

        internal static bool TryResetFriendlyFireScores(out string message)
        {
            message = L.T("Only the host can reset K/D/A.");
            if (!NetworkServer.active) return false;
            // Match reset also clears retained offline ranking entries. Do not
            // fall through from an expired/closing match into the casual path.
            if (CombatMatchRuntime.IsRunning || CombatMatchRuntime.RecoveryPending)
                return CombatMatchRuntime.ResetScores(out message);
            message = L.T("K/D/A reset requires a current hosted session that is not ending.");
            if (!EnsureResourceScope() || !CanResetFriendlyFireScores) return false;
            // Score-only: no policy write, reconciliation, selection edit, or
            // native revival. Loading players and unrelated faults cannot veto it.
            FriendlyFireKda.ResetScores();
            message = L.T("Friendly-fire K/D/A scores reset.");
            try { dungeon.Chat(null, "SephiriaOne", message); }
            catch (System.Exception error) { UnityEngine.Debug.LogWarning("[SephiriaOne] Score reset notice failed: " + error); }
            return true;
        }

        internal static bool TryExecuteFriendlyFire(FriendlyFireCommand command, out string message)
        {
            message = L.T("Only the host can change friendly fire.");
            if (!NetworkServer.active) return false;
            bool interrupted = CombatMatchRuntime.IsRunning && (!command.Percent.HasValue || command.Reset);
            var previousDungeon = DungeonManager.Instance; var previousRun = SaveManager.CurrentRun; long previousGeneration = ResourceGeneration;
            if (!command.Percent.HasValue || command.Reset) CombatMatchRuntime.Stop(true, true);
            if (!command.IsReset && !FriendlyFireFeature.Available)
            { message = L.T("Friendly-fire compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (interrupted)
            {
                // Stopping a match and selecting its toggle are policy-only.
                // A loading guest or unrelated stat fault cannot veto this.
                message = L.T("Session changed. Review the values and enter the action again.");
                if (!EnsureResourceScope() || !ReferenceEquals(previousDungeon, DungeonManager.Instance) ||
                    !ReferenceEquals(previousRun, SaveManager.CurrentRun) || previousGeneration != ResourceGeneration) return false;
                policy.Record(command); if (command.Reset) FriendlyFireParticipants.ResetSelection();
                FriendlyFireKda.SetEnabled(policy.FriendlyFire.Enabled);
                DeathmatchChanged(); message = DescribeFriendlyFire(policy.FriendlyFire); return true;
            }
            if (!PrepareCommand("combat", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("combat", context.CreateBatch(), () =>
                { policy.Record(command); if (command.Reset) FriendlyFireParticipants.ResetSelection();
                    FriendlyFireKda.SetEnabled(CombatMatchRuntime.ScoresEnabled(CombatMatchRuntime.Effective(policy.FriendlyFire).Enabled)); }, out message)) return false;
            message = DescribeFriendlyFire(CombatMatchRuntime.Effective(policy.FriendlyFire));
            return true;
        }

        internal static string DescribeFriendlyFire(FriendlyFireSettings settings) =>
            L.F("Friendly fire: {0}; allied damage: {1}%.", L.T(settings.Enabled ? "on" : "off"), settings.Number);
    }
}
