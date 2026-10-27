using Mirror;

namespace SephiriaOne
{
    internal static partial class SessionSettings
    {
        // Same event-time scope resolution as potions. Never cache per-player
        // policy or rely on the joining connection's prior incarnation.
        internal static FriendlyFireSettings FriendlyFireForHit => EnsureResourceScope() ? CombatMatchRuntime.Effective(policy.FriendlyFire) : default;
        internal static FriendlyFireSettings FriendlyFireSupplies => EnsureResourceScope() ? policy.FriendlyFire : default;
        internal static FriendlyFireSettings FriendlyFireForDisplay => enabled && NetworkServer.active &&
            dungeon && dungeon.isServer && dungeon.netId != 0 && ReferenceEquals(dungeon, DungeonManager.Instance) ? policy.FriendlyFire : default;
        internal static long CombatBalanceRevision { get; private set; }
        internal static long CombatFrequencyRevision { get; private set; }
        internal static long CombatRecoveryRevision { get; private set; }

        private static void RecordFriendlyFire(FriendlyFireCommand command)
        {
            bool previous = policy.FriendlyFire.Balance;
            bool previousFrequency = policy.FriendlyFire.HitFrequency;
            var before = policy.FriendlyFire;
            policy.Record(command);
            if (before.Enabled != policy.FriendlyFire.Enabled || before.AutoPotion != policy.FriendlyFire.AutoPotion)
                PvpPotionSupplies.SettingsChanged();
            if (previous != policy.FriendlyFire.Balance || command.Reset) CombatBalanceRevision++;
            if (previousFrequency != policy.FriendlyFire.HitFrequency || !before.Rapid.SameFrequency(policy.FriendlyFire.Rapid) || command.Reset) CombatFrequencyRevision++;
            if (!before.Rapid.SameRecovery(policy.FriendlyFire.Rapid) || command.Reset) CombatRecoveryRevision++;
            if (command.Reset) FriendlyFireParticipants.ResetSelection();
        }

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
            // Retired command: accept old scripts without mutating policy, scores,
            // matches or automatic supply state. Preset migration is separate.
            if (command.BlockRabbitInfinite.HasValue)
            { message = L.T("The Rabbit PvP override was removed. Use /one friendlyfire infinite-hp on|off; it controls HP potion consumption for all PvP participants. Automatic grants are separate."); return true; }
            if (command.Elements.HasValue && !command.Elements.Value.IsValid)
            { message = FriendlyFireCommand.Usage; return false; }
            if (command.Amplification.HasValue && !command.Amplification.Value.IsValid)
            { message = FriendlyFireCommand.Usage; return false; }
            if (command.Rapid.HasValue && !command.Rapid.Value.IsValid)
            { message = FriendlyFireCommand.Usage; return false; }
            if (command.LowerBound.HasValue && !command.LowerBound.Value.IsValid)
            { message = FriendlyFireCommand.Usage; return false; }
            if (command.ArtifactRate.HasValue && !command.ArtifactRate.Value.IsValid)
            { message = FriendlyFireCommand.Usage; return false; }
            if (command.PerfectGuard.HasValue && !command.PerfectGuard.Value.IsValid)
            { message = FriendlyFireCommand.Usage; return false; }
            if (command.DotMaxDurationSeconds.HasValue && !PvpDotSettings.ValidSeconds(command.DotMaxDurationSeconds.Value))
            { message = FriendlyFireCommand.Usage; return false; }
            bool interrupted = CombatMatchRuntime.IsRunning && command.TouchesMasterToggle;
            var previousDungeon = DungeonManager.Instance; var previousRun = SaveManager.CurrentRun; long previousGeneration = ResourceGeneration;
            if (command.TouchesMasterToggle) CombatMatchRuntime.Stop(true, true);
            if (!command.IsReset && !FriendlyFireFeature.Available)
            { message = L.T("Friendly-fire compatibility checks failed. Off/reset remain available; see Player.log."); return false; }
            if (command.InfiniteHpPotions == true && !RabbitPotionFeature.Available)
            { message = L.T("HP potion hooks unavailable; off/reset remain available. See Player.log."); return false; }
            if (command.AutoPotion == true && !LargePotionGrant.Available)
            { message = L.T("Large Potion is unavailable; see Player.log."); return false; }
            if (command.CombatMode == true && !CombatModeFeature.Available)
            { message = L.T("Combat Mode unavailable; off/reset remain available. See Player.log."); return false; }
            if (command.BlockLobbyRegen == true && !LobbyRegenFeature.Available)
            { message = L.T("Lobby recovery hooks unavailable; off/reset remain available. See Player.log."); return false; }
            if (command.CombatMode.HasValue)
            {
                if (!EnsureResourceScope()) { message = L.T("Host session is not ready."); return false; }
                RecordFriendlyFire(command); DeathmatchChanged();
                message = L.F("Combat Mode: {0}.", L.T(policy.FriendlyFire.CombatMode ? "on" : "off")); return true;
            }
            if (interrupted)
            {
                // Stopping a match and selecting its toggle are policy-only.
                // A loading guest or unrelated stat fault cannot veto this.
                message = L.T("Session changed. Review the values and enter the action again.");
                if (!EnsureResourceScope() || !ReferenceEquals(previousDungeon, DungeonManager.Instance) ||
                    !ReferenceEquals(previousRun, SaveManager.CurrentRun) || previousGeneration != ResourceGeneration) return false;
                RecordFriendlyFire(command);
                FriendlyFireKda.SetEnabled(policy.FriendlyFire.Enabled);
                DeathmatchChanged(); message = DescribeFriendlyFire(policy.FriendlyFire); return true;
            }
            if (!PrepareCommand("combat", command.IsReset, out HostCommandContext context, out message)) return false;
            if (!Commit("combat", context.CreateBatch(), () =>
                { RecordFriendlyFire(command);
                    FriendlyFireKda.SetEnabled(CombatMatchRuntime.ScoresEnabled(CombatMatchRuntime.Effective(policy.FriendlyFire).Enabled)); }, out message)) return false;
            message = DescribeFriendlyFire(CombatMatchRuntime.Effective(policy.FriendlyFire));
            return true;
        }

        internal static string DescribeFriendlyFire(FriendlyFireSettings settings) =>
            L.F("Friendly fire: {0}; allied damage: {1}%.", L.T(settings.Enabled ? "on" : "off"), settings.Number) + " " +
            (settings.Enabled ? L.T("PvP recovery: HP Steal is disabled on friendly-fire hits.") + " " + DescribeRabbitPvpSharing(RabbitPotionsForDisplay) + " " : "") +
            L.F("Combat Mode: {0}.", L.T(settings.CombatMode ? "on" : "off")) + " " +
            L.F("Infinite PvP HP potions: {0}.", L.T(settings.InfiniteHpPotions ? "on" : "off")) + " " +
            L.T("PvP potion consumption also applies to Rabbit. Grants are separate.") + " " +
            L.F("Automatic Large Potion: {0}.", L.T(settings.AutoPotion ? "on" : "off")) + " " +
            L.F("PvP balancing: {0}.", L.T(settings.Balance ? "on" : "off")) + " " +
            L.F("PvP lower-bound assistance: {0}; reference {1}% opponent max HP; shared bonus refill {2}% max HP/s (requires PvP balancing).",
                L.T(settings.LowerBound.Enabled ? "on" : "off"), settings.LowerBound.ReferenceNumber, settings.LowerBound.RefillNumber) + " " +
            L.F("Suppress PvP Thorns: {0} (requires PvP balancing).", L.T(settings.SuppressThorns ? "on" : "off")) + " " +
            L.F("Hit-frequency scaling: {0} (requires PvP balancing).", L.T(settings.HitFrequency ? "on" : "off")) + " " +
            L.F("Rapid history: {0}s; full-damage threshold: {1} hits/s; reduction exponent: {2}; burst allowance: {3}% max HP.",
                settings.Rapid.WindowNumber, settings.Rapid.RateNumber, settings.Rapid.ExponentNumber, settings.Rapid.BurstNumber) + " " +
            L.F("Periodic recovery: {0}; every {1}s restores shared burst and clears hit history. Continuous refill remains active.",
                L.T(settings.Rapid.PeriodicReset ? "on" : "off"), settings.Rapid.ResetNumber) + " " +
            L.F("PvP refill: {0}% max HP/s.", settings.RefillNumber) + " " +
            L.F("Allowance refills continuously; base empty-to-full recovery: {0}s. All hit types share one reserve; native defenses, role modifiers and debuff schedules still apply. Minimum hits can exceed the allowance.",
                (settings.Rapid.BurstPercent / settings.RefillPercent).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)) + " " +
            L.F("PvP shield minimum: {0}% max MP after native reductions.", settings.GuardMpNumber) + " " +
            L.F("Perfect Guard MP multiplier: {0}; x{1} native MP cost (requires PvP balancing; when on, ignores shield minimum).",
                L.T(settings.PerfectGuard.Enabled ? "on" : "off"), settings.PerfectGuard.Number) + " " +
            L.F("Elemental stat limits: {0}; Physical {1}/{2}, Flame {3}/{4}, Ice {5}/{6}, Lightning {7}/{8} (soft/hard). Effective participant stats while PvP balancing is on; participant PvE damage is also affected. Saved stats stay unchanged.",
                L.T(settings.Elements.Enabled ? "on" : "off"),
                settings.Elements.GetLimit(PvpElement.Physical).SoftNumber, settings.Elements.GetLimit(PvpElement.Physical).HardNumber,
                settings.Elements.GetLimit(PvpElement.Flame).SoftNumber, settings.Elements.GetLimit(PvpElement.Flame).HardNumber,
                settings.Elements.GetLimit(PvpElement.Ice).SoftNumber, settings.Elements.GetLimit(PvpElement.Ice).HardNumber,
                settings.Elements.GetLimit(PvpElement.Lightning).SoftNumber, settings.Elements.GetLimit(PvpElement.Lightning).HardNumber) + " " +
            L.F("All Damage Amplified limit: {0}; soft {1}%, hard {2}% bonus (100% = +100%; requires PvP balancing).",
                L.T(settings.Amplification.Enabled ? "on" : "off"), settings.Amplification.SoftNumber, settings.Amplification.HardNumber) + " " +
            L.F("PvP DoT intervals (s): Burn {0}; Plasma Burn {1}; Frostbite {2}; Poison {3}.",
                settings.DotIntervals.Number(PvpDotEffect.Burn), settings.DotIntervals.Number(PvpDotEffect.PlasmaBurn),
                settings.DotIntervals.Number(PvpDotEffect.Frostbite), settings.DotIntervals.Number(PvpDotEffect.Poison)) + " " +
            L.F("PvP effect intervals (s): Wound {0}; Lightning/Shock {1}; Freeze damage {2}; Chim {3}; Dark Cloud {4}.",
                settings.DotIntervals.Number(PvpDotEffect.Wound), settings.DotIntervals.Number(PvpDotEffect.Lightning),
                settings.DotIntervals.Number(PvpDotEffect.Freeze), settings.DotIntervals.Number(PvpDotEffect.Chim),
                settings.DotIntervals.Number(PvpDotEffect.DarkCloud)) + " " +
            L.F("Plasma shock interval: {0}s.", settings.DotIntervals.Number(PvpDotEffect.PlasmaShock)) + " " +
            L.F("Autonomous artifact hit limit: {0}; {1}s per artifact/opponent (requires PvP balancing; excludes Dark Cloud).",
                L.T(settings.ArtifactRate.Enabled ? "on" : "off"), settings.ArtifactRate.Number) + " " +
            L.F("PvP Burn-family duration cap: {0}s (Burn, Plasma Burn and blue/evolved variants; requires balancing).", settings.DotMaxDurationNumber) + " " +
            DescribeDebuffSchedules(settings.DotIntervals) + " " +
            L.F("Block lobby HP/MP recovery: {0}.", L.T(settings.BlockLobbyRegen ? "on" : "off")) +
            (settings.BlockLobbyRegen && !LobbyRegenFeature.Available ? " " + L.T("Lobby recovery hooks unavailable; off/reset remain available. See Player.log.") : "");

        private static string DescribeDebuffSchedules(PvpDotSettings settings)
        {
            var values = new System.Text.StringBuilder();
            for (int i = 0; i < PvpDotSettings.Count; i++)
            {
                var effect = (PvpDotEffect)i;
                if (!PvpDotSettings.SupportsSchedule(effect)) continue;
                if (i != 0) values.Append("; ");
                values.Append(PvpDotSettings.Key(effect)).Append(' ').Append(L.T(settings.PreserveSchedule(effect) ? "on" : "off"));
            }
            return L.F("Keep debuff schedules: {0} (requires PvP balancing).", values.ToString());
        }
    }
}
