using System.Runtime.CompilerServices;
using UnityEngine;

namespace SephiriaOne
{
    internal static partial class FriendlyFireRuntime
    {
        private sealed class BalanceActor
        {
            internal long Life;
            internal EvadeRefundWindow EvadeRefund;
            internal readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow> Targets =
                new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow>();
        }

        internal sealed class BalanceWindow
        {
            internal bool Started;
            internal long CombatEpoch, ParticipantEpoch, Revision, SourceLife, TargetLife, Generation, RoleRevision;
            internal string SourceFloor, TargetFloor;
            internal FriendlyFireDamageBudget Budget;
            internal FriendlyFireDamageBudget Assistance;
            internal FriendlyFireHitFrequency Frequency, IncidentalFrequency;
            private FriendlyFireHitFrequency[] planets;
            internal long FrequencyRevision;
            internal ref FriendlyFireHitFrequency PlanetFrequency(int source)
            {
                if (planets == null) planets = new FriendlyFireHitFrequency[PlanetHitCadence.Count];
                return ref planets[source];
            }
            internal void ResetFrequencies()
            {
                Frequency = IncidentalFrequency = default;
                if (planets != null) System.Array.Clear(planets, 0, planets.Length);
            }
            // Fixed catalog-sized storage, allocated only for a pair that lands
            // an audited artifact hit. Re-equipping cannot reset source identity.
            private DebuffPulse[] artifacts;
            internal bool ArtifactReady(int source, float now, float interval) =>
                artifacts == null || artifacts[source].Ready(now, interval);
            internal void ReserveArtifact(int source, float now)
            {
                if (artifacts == null) artifacts = new DebuffPulse[AutonomousArtifactCatalog.Count];
                artifacts[source].Reserve(now);
            }
            internal void ResetArtifacts() { if (artifacts != null) System.Array.Clear(artifacts, 0, artifacts.Length); }
            private DebuffPulse burn, plasma, frostbite, poison, wound, shock, freeze, chim, plasmaShock;
            internal ref DebuffPulse Pulse(PvpDotEffect effect)
            {
                switch (effect)
                {
                    case PvpDotEffect.Burn: return ref burn;
                    case PvpDotEffect.PlasmaBurn: return ref plasma;
                    case PvpDotEffect.Frostbite: return ref frostbite;
                    case PvpDotEffect.Poison: return ref poison;
                    case PvpDotEffect.Wound: return ref wound;
                    case PvpDotEffect.Lightning: return ref shock;
                    case PvpDotEffect.PlasmaShock: return ref plasmaShock;
                    case PvpDotEffect.Freeze: return ref freeze;
                    case PvpDotEffect.Chim: return ref chim;
                    default: throw new System.ArgumentOutOfRangeException(nameof(effect));
                }
            }
            internal void ResetPulses() => burn = plasma = frostbite = poison = wound = shock = freeze = chim = plasmaShock = default;
        }

        private static ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor> balanceActors =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>();
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>.CreateValueCallback createBalanceActor =
            NewBalanceActor;
        private static readonly ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceWindow>.CreateValueCallback createBalanceWindow =
            NewBalanceWindow;
        private static BalanceActor NewBalanceActor(FriendlyFireParticipants.Receipt _) => new BalanceActor();
        private static BalanceWindow NewBalanceWindow(FriendlyFireParticipants.Receipt _) => new BalanceWindow();

        // Strike admission can precede damage-budget creation (or emit no hit).
        // Register both actors here so revival also retires those strike windows.
        private static long BalanceLife(FriendlyFireParticipants.Receipt receipt) => balanceActors.GetValue(receipt, createBalanceActor).Life;

        private static void ClearBalanceBudgets() => balanceActors =
            new ConditionalWeakTable<FriendlyFireParticipants.Receipt, BalanceActor>();

        // A dead avatar cannot deal new attacks before revival succeeds. Advance
        // its life here so immediate native/manual revivals do not inherit debt.
        private static void ResetBalanceLife(PlayerAvatar player)
        {
            var receipt = FriendlyFireParticipants.Capture(player);
            if (receipt != null && balanceActors.TryGetValue(receipt, out BalanceActor actor)) actor.Life++;
        }

        private static BalanceWindow CurrentBalanceWindow()
        {
            var attacker = current.AttackerReceipt;
            var victim = current.VictimReceipt;
            if (attacker == null || victim == null) return null;
            var source = balanceActors.GetValue(attacker, createBalanceActor);
            var target = balanceActors.GetValue(victim, createBalanceActor);
            var window = source.Targets.GetValue(victim, createBalanceWindow);
            long epoch = FriendlyFireKda.Epoch, participants = FriendlyFireParticipants.Epoch;
            long revision = SessionSettings.CombatBalanceRevision;
            string sourceFloor = attacker.Player.currentFloorGuid, targetFloor = victim.Player.currentFloorGuid;
            if (!window.Started || window.CombatEpoch != epoch || window.ParticipantEpoch != participants ||
                window.Revision != revision || window.SourceLife != source.Life || window.TargetLife != target.Life ||
                window.SourceFloor != sourceFloor || window.TargetFloor != targetFloor)
            {
                window.Started = true; window.Budget = window.Assistance = default; window.Generation++;
                window.ResetPulses(); window.ResetArtifacts();
                window.ResetFrequencies();
                window.CombatEpoch = epoch; window.ParticipantEpoch = participants; window.Revision = revision;
                window.SourceLife = source.Life; window.TargetLife = target.Life;
                window.SourceFloor = sourceFloor; window.TargetFloor = targetFloor;
            }
            return window;
        }

        private static double LimitBalancedDamage(double damage, double maxHp, double refillPerSecond, double baseRefill, double incomingScale, bool hitFrequency, PvpDotSettings intervals, PvpRapidSettings rapid, PvpArtifactRateSettings artifactRate, PvpLowerBoundSettings lowerBound)
        {
            var window = CurrentBalanceWindow();
            if (window == null) return 0;
            // Reserve before later native callbacks; nested procs and companions
            // consume the owner's same directed budget, regardless of damage ID.
            float now = Time.time;
            int artifact = CurrentAutonomousArtifact();
            // A native callback can land a nested hit after our early read-only
            // admission. Recheck here before rate histories or damage credit.
            if (artifact >= 0 && artifactRate.Enabled && !window.ArtifactReady(artifact, now, artifactRate.Interval)) return 0;
            int pulse = DebuffPulseKind(current.Damage);
            var effect = (PvpDotEffect)pulse;
            if (pulse >= 0 && intervals.PreserveSchedule(effect) &&
                !window.Pulse(effect).Ready(now, intervals.Interval(effect))) return 0;
            double requestedBonus = lowerBound.Enabled ? FriendlyFireLowerBound.RequestedBonus(damage, maxHp, lowerBound.ReferenceScale) : 0;
            if (hitFrequency && damage > 0)
            {
                long frequencyRevision = SessionSettings.CombatFrequencyRevision;
                if (window.FrequencyRevision != frequencyRevision)
                { window.ResetFrequencies(); window.FrequencyRevision = frequencyRevision; }
                // Each audited Planet type has its own cadence: independent
                // orbiters and their procs must not slow one another. All copies
                // of that type still share history, and every hit pays the same
                // owner/opponent budget below. Other incidental policy is unchanged.
                int planet = ReferenceEquals(current.Source, current.Attacker) && !current.Reflection &&
                    !current.InDebuffDamage && !current.InBurnExplosion && !current.InArtifactDamage
                    ? PlanetHitCadence.Source(current.Damage) : -1;
                ref var frequency = ref (planet >= 0 ? ref window.PlanetFrequency(planet) :
                    ref (IsIncidentalHit() ? ref window.IncidentalFrequency : ref window.Frequency));
                double factor = frequency.Next(now, rapid);
                damage *= factor;
                requestedBonus *= factor;
            }
            // One bonus reserve for the whole owner/opponent, not per source or
            // cadence category. Charge in post-frequency, pre-role damage units.
            double bonus = requestedBonus > 0 ? window.Assistance.Take(requestedBonus, maxHp, now,
                refillPerSecond: lowerBound.RefillScale, capacity: lowerBound.ReferenceScale) : 0;
            long chaserRevision = CombatMatchRuntime.RoleRevision;
            // A carrier can change away and back between two hits. Preserve tokens,
            // but do not award boosted credit across an unobserved role/config edit.
            double elapsedCeiling = window.RoleRevision == chaserRevision ? double.PositiveInfinity : baseRefill;
            // Reserve funded credit before callbacks. The minimum survives an
            // empty pool; role reduction still uses pre-reduction ledger units.
            double allowed = window.Budget.Take(damage + bonus, maxHp, now, out double charged, refillPerSecond, elapsedCeiling, 1d / incomingScale, rapid.BurstScale);
            // Original damage spends the upper allowance first. Do not burn
            // bonus credit that the upper budget could not actually fund.
            double fundedBonus = System.Math.Min(bonus, System.Math.Max(0, charged - damage));
            if (bonus > fundedBonus) window.Assistance.Refund((bonus - fundedBonus) / maxHp, lowerBound.ReferenceScale);
            window.RoleRevision = chaserRevision;
            if (allowed > 0)
            {
                // Reserve before callbacks, including floor-only hits. The existing
                // receipt/life/epoch/floor reset owns both clocks and tokens.
                // Keep history even when protection is off; enabling it live
                // must not award an extra immediate hit or reset the budget.
                if (pulse >= 0) window.Pulse(effect).Reserve(now);
                if (artifact >= 0) window.ReserveArtifact(artifact, now);
                current.BudgetWindow = window; current.BudgetGeneration = window.Generation;
                current.BudgetCharged = charged; current.BudgetMaxHp = maxHp;
                current.BudgetIncomingScale = incomingScale;
                current.BudgetBonusCharged = fundedBonus;
            }
            return allowed;
        }

        private static bool IsIncidentalHit() =>
            !ReferenceEquals(current.Source, current.Attacker) || current.Reflection ||
            current.InDebuffDamage || current.InBurnExplosion || current.InArtifactDamage ||
            current.Damage.fromType == EDamageFromType.None ||
            current.Damage.damageType == EDamageType.ElementalEffectDamage ||
            // Async artifact projectiles can carry player-like source tags.
            // Read the current ID: pooled damage objects must not cache a category.
            current.Damage.id != null && current.Damage.id.StartsWith("Charm_", System.StringComparison.Ordinal);

        private static void FinishBalancedDamage()
        {
            var window = current.BudgetWindow;
            if (window == null) return;
            // Tokens use pre-chaser-reduction damage units. Refund only genuine
            // native losses (MP rounding, veto, armor, overkill), not the intended
            // chaser reduction; otherwise a rapid barrage would refund it away.
            double unused = current.BudgetCharged - current.BudgetAccounted / current.BudgetIncomingScale;
            // The usual fully delivered hit needs no additional lifecycle query.
            // Native damage is float precision. A double-to-float residue is not
            // real unused credit and must not manufacture extra funded allowance.
            if (unused <= current.BudgetCharged * 1e-7d || current.BudgetGeneration != window.Generation || !CurrentHitPermitted()) return;
            // Native shields and HP report their own applied amounts. Preserve
            // fractional credit lost to MP integer rounding or a later veto.
            // Never refund an exceptional hit: it may already have written HP.
            window.Budget.Refund(unused / current.BudgetMaxHp, SessionSettings.FriendlyFireForHit.Rapid.BurstScale);
            // Native veto/overkill losses return the bonus portion first, once.
            // Shield absorption is already accounted as delivered damage above.
            double unusedBonus = System.Math.Min(unused, current.BudgetBonusCharged);
            if (unusedBonus > 0) window.Assistance.Refund(unusedBonus / current.BudgetMaxHp,
                SessionSettings.FriendlyFireForHit.LowerBound.ReferenceScale);
        }
    }
}
