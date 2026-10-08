#nullable enable
using System;
using System.Collections.Generic;

namespace SephiriaOne
{
    [Flags]
    internal enum SyncDomain
    {
        None = 0, Identity = 1, Stats = 2, Fountain = 4, Choices = 8,
        Limits = 16, Names = 32, Resources = 64, All = 127
    }

    internal enum ReconcileMode { Once, OnChange }
    internal enum ReconcileState { WaitingForReadiness, Applied, NativeFallback, Suspended, Rejected, Faulted }

    internal readonly struct ReconcileResult
    {
        public ReconcileState State { get; }
        public string Detail { get; }
        // Only a verified application/restoration is safe for native consumers.
        // Suspended, rejected, waiting, and partial/faulted writes remain unready.
        public bool IsFresh => State == ReconcileState.Applied || State == ReconcileState.NativeFallback;
        private ReconcileResult(ReconcileState state, string detail) { State = state; Detail = detail; }
        public static ReconcileResult Applied(string detail = "") => new ReconcileResult(ReconcileState.Applied, detail);
        public static ReconcileResult NativeFallback(string detail) => new ReconcileResult(ReconcileState.NativeFallback, detail);
        public static ReconcileResult Waiting(string detail) => new ReconcileResult(ReconcileState.WaitingForReadiness, detail);
        public static ReconcileResult Suspended(string detail) => new ReconcileResult(ReconcileState.Suspended, detail);
        public static ReconcileResult Rejected(string detail) => new ReconcileResult(ReconcileState.Rejected, detail);
        public static ReconcileResult Faulted(string detail) => new ReconcileResult(ReconcileState.Faulted, detail);
    }

    internal readonly struct ReconciliationStatus
    {
        public string Id { get; }
        public ReconcileState State { get; }
        public string Detail { get; }
        public long Revision { get; }
        internal ReconciliationStatus(string id, ReconcileResult result, long revision)
        { Id = id; State = result.State; Detail = result.Detail; Revision = revision; }
    }

    internal sealed class ReconciliationRule<T> where T : class
    {
        public string Id { get; }
        public SyncDomain Inputs { get; }
        public SyncDomain Outputs { get; }
        public ReconcileMode Mode { get; }
        internal Func<T, bool> IsReady { get; }
        internal Func<IReconciliationObservation<T>> CreateObservation { get; }
        internal Func<T, ReconcileResult> Apply { get; }

        public ReconciliationRule(string id, SyncDomain inputs, SyncDomain outputs, ReconcileMode mode,
            Func<T, bool> isReady, Func<T, object> observe, Func<T, ReconcileResult> apply)
            : this(id, inputs, outputs, mode, isReady, () => new Observation<object>(observe), apply)
        { if (observe == null) throw new ArgumentNullException(nameof(observe)); }

        // Each subject owns its cursor. Value snapshots stay typed instead of
        // boxing on every unchanged frame; equality still includes every input.
        public static ReconciliationRule<T> ObserveValue<TValue>(string id, SyncDomain inputs, SyncDomain outputs, ReconcileMode mode,
            Func<T, bool> isReady, Func<T, TValue> observe, Func<T, ReconcileResult> apply)
        {
            if (observe == null) throw new ArgumentNullException(nameof(observe));
            return new ReconciliationRule<T>(id, inputs, outputs, mode, isReady, () => new Observation<TValue>(observe), apply);
        }

        private ReconciliationRule(string id, SyncDomain inputs, SyncDomain outputs, ReconcileMode mode,
            Func<T, bool> isReady, Func<IReconciliationObservation<T>> createObservation, Func<T, ReconcileResult> apply)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A rule ID is required.", nameof(id));
            Id = id; Inputs = inputs; Outputs = outputs; Mode = mode;
            IsReady = isReady ?? throw new ArgumentNullException(nameof(isReady));
            CreateObservation = createObservation;
            Apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }

        private sealed class Observation<TValue> : IReconciliationObservation<T>
        {
            private readonly Func<T, TValue> capture;
            private TValue previous = default!;
            private bool observed;
            public Observation(Func<T, TValue> capture) { this.capture = capture; }
            public bool Capture(T subject)
            {
                TValue current = capture(subject);
                bool changed = !observed || !EqualityComparer<TValue>.Default.Equals(current, previous);
                previous = current; observed = true;
                return changed;
            }
        }
    }

    internal interface IReconciliationObservation<T> { bool Capture(T subject); }
}
