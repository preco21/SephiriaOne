#nullable enable
using System;

namespace SephiriaOne
{
    [Flags]
    internal enum SyncDomain
    {
        None = 0, Identity = 1, Stats = 2, Fountain = 4, Choices = 8,
        Limits = 16, Names = 32, Resources = 64, All = 127
    }

    internal enum ReconcileMode { Once, OnChange }
    internal enum ReconcileState { WaitingForReadiness, Applied, Suspended, Rejected, Faulted }

    internal readonly struct ReconcileResult
    {
        public ReconcileState State { get; }
        public string Detail { get; }
        private ReconcileResult(ReconcileState state, string detail) { State = state; Detail = detail; }
        public static ReconcileResult Applied(string detail = "") => new ReconcileResult(ReconcileState.Applied, detail);
        public static ReconcileResult Waiting(string detail) => new ReconcileResult(ReconcileState.WaitingForReadiness, detail);
        public static ReconcileResult Suspended(string detail) => new ReconcileResult(ReconcileState.Suspended, detail);
        public static ReconcileResult Rejected(string detail) => new ReconcileResult(ReconcileState.Rejected, detail);
        public static ReconcileResult Faulted(string detail) => new ReconcileResult(ReconcileState.Faulted, detail);
    }

    internal sealed class ReconciliationStatus
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
        internal Func<T, object> Observe { get; }
        internal Func<T, ReconcileResult> Apply { get; }

        public ReconciliationRule(string id, SyncDomain inputs, SyncDomain outputs, ReconcileMode mode,
            Func<T, bool> isReady, Func<T, object> observe, Func<T, ReconcileResult> apply)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A rule ID is required.", nameof(id));
            Id = id; Inputs = inputs; Outputs = outputs; Mode = mode;
            IsReady = isReady ?? throw new ArgumentNullException(nameof(isReady));
            Observe = observe ?? throw new ArgumentNullException(nameof(observe));
            Apply = apply ?? throw new ArgumentNullException(nameof(apply));
        }
    }
}
