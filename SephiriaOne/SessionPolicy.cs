#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    internal sealed class SessionPlayerSnapshot
    {
        public IReadOnlyDictionary<string, int> Raw { get; }
        public IReadOnlyDictionary<string, int> Bonus { get; }
        public IReadOnlyDictionary<string, int> Amplifiers { get; }
        public int FountainPoints { get; }
        public int FountainContribution { get; }
        public int? FountainLimit { get; }
        public int? OriginalLimit { get; }
        public int? AppliedLimit { get; }
        public bool ChoicesAvailable { get; }

        public SessionPlayerSnapshot(IReadOnlyDictionary<string, int> raw, IReadOnlyDictionary<string, int> bonus,
            IReadOnlyDictionary<string, int> amplifiers, int fountainPoints, int fountainContribution,
            int? fountainLimit, int? originalLimit, int? appliedLimit, bool choicesAvailable)
        {
            Raw = raw; Bonus = bonus; Amplifiers = amplifiers;
            FountainPoints = fountainPoints; FountainContribution = fountainContribution;
            FountainLimit = fountainLimit; OriginalLimit = originalLimit; AppliedLimit = appliedLimit;
            ChoicesAvailable = choicesAvailable;
        }

        public static int Read(IReadOnlyDictionary<string, int> values, string key) =>
            values.TryGetValue(key, out int value) ? value : 0;
    }

    internal readonly struct SessionStatWrite
    {
        public string Key { get; }
        public string Marker { get; }
        public int Raw { get; }
        public int Contribution { get; }
        public SessionStatWrite(string key, string marker, int raw, int contribution)
        {
            Key = key; Marker = marker; Raw = raw; Contribution = contribution;
        }
    }

    internal sealed class SessionPlan
    {
        public FountainPlan? Fountain { get; }
        public IReadOnlyList<SessionStatWrite> Stats { get; }
        public SessionPlan(FountainPlan? fountain, IReadOnlyList<SessionStatWrite> stats)
        {
            Fountain = fountain; Stats = stats;
        }
    }

    internal sealed partial class SessionPolicy
    {
        private readonly struct Setting
        {
            public bool Absolute { get; }
            public decimal Value { get; }
            public bool Empty => !Absolute && Value == 0;
            public Setting(bool absolute, decimal value) { Absolute = absolute; Value = value; }
            public Setting Add(decimal delta) => new Setting(Absolute, Value + delta);
        }

        private Setting? fountain;
        private readonly Dictionary<string, int> choices = new Dictionary<string, int>();
        private readonly Dictionary<StatDefinition, Setting> stats = new Dictionary<StatDefinition, Setting>();
        public bool HasChanges => fountain.HasValue || choices.Count != 0 || stats.Count != 0;
        public bool HasFountainSetting => fountain.HasValue;

        // Call only after a host command succeeds. Failed requests never become policy.
        public void Record(FountainCommand command)
        {
            if (command.Operation == FountainOperation.Reset) { fountain = null; return; }
            Setting next = command.Operation == FountainOperation.Set ? new Setting(true, command.Amount) :
                fountain.GetValueOrDefault().Add(command.Operation == FountainOperation.Add ? command.Amount : -(decimal)command.Amount);
            fountain = next.Empty ? (Setting?)null : next;
        }

        public void Record(StatCommand command)
        {
            if (command.Operation == StatOperation.Reset)
            {
                if (command.Stat == null) stats.Clear();
                else stats.Remove(command.Stat);
                return;
            }
            if (command.Stat == null) throw new ArgumentException("A stat is required.", nameof(command));
            stats.TryGetValue(command.Stat, out Setting current);
            Setting next = command.Operation == StatOperation.Set ? new Setting(true, command.Amount) :
                current.Add(command.Operation == StatOperation.Add ? command.Amount : -command.Amount);
            if (next.Empty) stats.Remove(command.Stat);
            else stats[command.Stat] = next;
        }

        // The successful batch already calculated the active addon contribution.
        public void RecordChoice(string key, int contribution)
        {
            if (Array.IndexOf(ChoiceCommand.Keys, key) < 0 || contribution < 0 || contribution > ChoiceCommand.MaximumExtra)
                throw new ArgumentOutOfRangeException(nameof(contribution));
            if (contribution == 0) choices.Remove(key);
            else choices[key] = contribution;
        }

        public void Clear()
        {
            fountain = null;
            choices.Clear();
            stats.Clear();
        }

        public bool TryPlan(SessionPlayerSnapshot player, out SessionPlan plan, out string error)
        {
            plan = new SessionPlan(null, Array.Empty<SessionStatWrite>());
            var writes = new List<SessionStatWrite>();
            FountainPlan? fountainPlan = null;
            error = "The joining player's Fountain baseline or session limit is unavailable.";
            if (fountain.HasValue)
            {
                Setting setting = fountain.Value;
                long baseline = (long)player.FountainPoints - player.FountainContribution;
                if (!player.FountainLimit.HasValue || baseline < 0 || baseline > int.MaxValue ||
                    setting.Value < -int.MaxValue || setting.Value > int.MaxValue) return false;
                var command = new FountainCommand(setting.Absolute ? FountainOperation.Set :
                    setting.Value < 0 ? FountainOperation.Subtract : FountainOperation.Add,
                    (int)(setting.Absolute ? setting.Value : Math.Abs(setting.Value)));
                if (!command.TryPlanTracked(new[] { (int)baseline }, new[] { 0 }, player.FountainLimit.Value,
                    player.OriginalLimit, player.AppliedLimit, out FountainPlan pending, out error)) return false;
                fountainPlan = pending;
            }

            error = "Candidate guards are unavailable; inherited settings were not applied.";
            if (choices.Count != 0 && !player.ChoicesAvailable) return false;
            foreach (var choice in choices)
            {
                int index = Array.IndexOf(ChoiceCommand.Keys, choice.Key);
                string marker = "SEPHIRIAONE_" + choice.Key;
                var command = new ChoiceCommand((ChoiceTarget)(1 << index), ChoiceOperation.Set, choice.Value);
                if (!command.TryPlan(SessionPlayerSnapshot.Read(player.Raw, choice.Key), SessionPlayerSnapshot.Read(player.Raw, marker),
                    SessionPlayerSnapshot.Read(player.Bonus, choice.Key), SessionPlayerSnapshot.Read(player.Amplifiers, choice.Key),
                    out int raw, out int applied, out error)) return false;
                writes.Add(new SessionStatWrite(choice.Key, marker, raw, applied));
            }

            foreach (var entry in stats)
            {
                StatDefinition stat = entry.Key;
                Setting setting = entry.Value;
                long baseline = (long)SessionPlayerSnapshot.Read(player.Raw, stat.Key) - SessionPlayerSnapshot.Read(player.Raw, stat.Marker);
                error = "The joining player's native stat baseline would overflow.";
                if (baseline < int.MinValue || baseline > int.MaxValue) return false;
                var command = new StatCommand(stat, setting.Absolute ? StatOperation.Set :
                    setting.Value < 0 ? StatOperation.Subtract : StatOperation.Add,
                    setting.Absolute ? setting.Value : Math.Abs(setting.Value));
                var snapshot = new StatSnapshot(stat, (int)baseline, 0, SessionPlayerSnapshot.Read(player.Bonus, stat.Key),
                    SessionPlayerSnapshot.Read(player.Amplifiers, stat.Key));
                if (!StatPlanner.TryPlan(command, new[] { snapshot }, out StatUpdate[] updates, out error)) return false;
                writes.Add(new SessionStatWrite(stat.Key, stat.Marker, updates[0].Raw, updates[0].Contribution));
            }
            plan = new SessionPlan(fountainPlan, writes);
            error = "";
            return true;
        }
    }

    internal sealed class SessionJoinTracker
    {
        private object? session;
        private readonly HashSet<uint> processed = new HashSet<uint>();

        public bool SetSession(object? current)
        {
            if (ReferenceEquals(session, current)) return false;
            session = current;
            processed.Clear();
            return true;
        }

        // Mark before attempting writes: exceptions/rejections must not spam or
        // repeatedly apply a bonus. New avatar IDs still get their own attempt.
        public bool TryBegin(uint avatarId, bool ready) => session != null && ready && avatarId != 0 && processed.Add(avatarId);
    }
}
