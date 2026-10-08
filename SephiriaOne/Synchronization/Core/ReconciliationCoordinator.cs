#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SephiriaOne
{
    // Main-thread scheduler. Subjects are object lifetimes, not reusable network IDs.
    internal sealed class ReconciliationCoordinator<T> where T : class
    {
        private sealed class Record
        {
            public readonly IReconciliationObservation<T> Observation;
            public bool Attempted;
            public long Requested;
            public long Handled;
            public long Revision;
            public ReconcileResult Result = ReconcileResult.Waiting("Not observed yet.");
            public Record(IReconciliationObservation<T> observation) { Observation = observation; }
        }

        private readonly List<ReconciliationRule<T>> rules = new List<ReconciliationRule<T>>();
        private readonly Dictionary<T, Dictionary<string, Record>> subjects =
            new Dictionary<T, Dictionary<string, Record>>(ReferenceComparer<T>.Instance);
        private bool processing;
        private long generation;
        public bool IsProcessing => processing;

        public void Register(ReconciliationRule<T> rule)
        {
            if (processing) throw new InvalidOperationException("Cannot register during reconciliation.");
            foreach (var existing in rules)
                if (existing.Id == rule.Id) throw new InvalidOperationException("Duplicate rule: " + rule.Id);
            var pending = new List<ReconciliationRule<T>>(rules) { rule };
            var sorted = new List<ReconciliationRule<T>>();
            while (pending.Count > 0)
            {
                int next = pending.FindIndex(candidate => !pending.Exists(other =>
                    !ReferenceEquals(candidate, other) && (other.Outputs & candidate.Inputs) != SyncDomain.None));
                if (next < 0) throw new InvalidOperationException("Reconciliation dependencies contain a cycle.");
                sorted.Add(pending[next]);
                pending.RemoveAt(next);
            }
            rules.Clear();
            rules.AddRange(sorted);
        }

        private Dictionary<string, Record> Records(T subject)
        {
            if (!subjects.TryGetValue(subject, out var records))
            { records = new Dictionary<string, Record>(); subjects.Add(subject, records); }
            foreach (var rule in rules)
                if (!records.ContainsKey(rule.Id)) records.Add(rule.Id, new Record(rule.CreateObservation()));
            return records;
        }

        public void Invalidate(T subject, SyncDomain inputs)
        {
            var records = Records(subject);
            foreach (var rule in rules)
                if ((rule.Inputs & inputs) != SyncDomain.None) records[rule.Id].Requested++;
        }

        // A separately validated command/recovery has already established this state.
        public void AcceptObservation(T subject, string id)
        {
            var rule = rules.Find(value => value.Id == id) ?? throw new ArgumentException("Unknown rule: " + id);
            Record record = Records(subject)[id];
            record.Observation.Capture(subject);
            record.Attempted = true;
            record.Handled = record.Requested;
            record.Result = ReconcileResult.Applied(); record.Revision++;
        }

        public bool Reconcile(T subject)
        {
            if (processing)
            {
                Invalidate(subject, SyncDomain.All);
                return false;
            }
            processing = true;
            long currentGeneration = generation;
            try
            {
                var records = Records(subject);
                for (int pass = 0; pass < 4; pass++)
                {
                    foreach (var rule in rules)
                    {
                        if (currentGeneration != generation || !subjects.ContainsKey(subject)) return false;
                        Record record = records[rule.Id];
                        if (rule.Mode == ReconcileMode.Once && record.Attempted)
                        { record.Handled = record.Requested; continue; }
                        long requested = record.Requested;
                        try
                        {
                            if (!rule.IsReady(subject))
                            {
                                record.Result = ReconcileResult.Waiting("Required state or authority is not ready.");
                                record.Handled = requested;
                                continue;
                            }
                            bool changed = record.Observation.Capture(subject);
                            if (!changed && requested == record.Handled &&
                                record.Result.State != ReconcileState.WaitingForReadiness) continue;
                            record.Result = rule.Apply(subject);
                            record.Attempted = record.Result.State != ReconcileState.WaitingForReadiness;
                            record.Revision++;
                            // Keep newer invalidation counters, including those raised by Apply.
                            record.Handled = requested;
                            record.Observation.Capture(subject);
                        }
                        catch (Exception error)
                        {
                            record.Result = ReconcileResult.Faulted(error.Message);
                            record.Attempted = true;
                            record.Handled = requested;
                            // Observe may itself fail. Preserve its previous snapshot safely.
                            try { record.Observation.Capture(subject); } catch { }
                        }
                    }
                    bool pending = false;
                    foreach (var record in records.Values) pending |= record.Requested != record.Handled;
                    if (!pending)
                    {
                        foreach (var record in records.Values)
                            if (!record.Result.IsFresh) return false;
                        return true;
                    }
                }
                return false; // A continuously invalidating rule is not a successful critical flush.
            }
            finally { processing = false; }
        }

        public IReadOnlyList<ReconciliationStatus> Describe(T subject)
        {
            var result = new List<ReconciliationStatus>();
            AppendStatuses(subject, result);
            return result;
        }

        // Copy only recorded outcomes. Do not create/capture observations here:
        // callers may inspect pending, forgotten or faulted subjects. The caller
        // owns the buffer; no native objects or mutable records are exposed.
        public void AppendStatuses(T subject, List<ReconciliationStatus> destination)
        {
            if (subjects.TryGetValue(subject, out var records))
                foreach (var rule in rules)
                    if (records.TryGetValue(rule.Id, out var record))
                        destination.Add(new ReconciliationStatus(rule.Id, record.Result, record.Revision));
        }

        public bool TryGetResult(T subject, string id, out ReconcileResult result)
        {
            if (subjects.TryGetValue(subject, out var records) && records.TryGetValue(id, out var record))
            { result = record.Result; return true; }
            result = default;
            return false;
        }

        public void Forget(T subject) => subjects.Remove(subject);
        public void Clear() { subjects.Clear(); generation++; }
    }

    internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
