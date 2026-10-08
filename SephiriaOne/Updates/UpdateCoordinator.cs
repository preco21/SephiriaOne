using System;
using System.Threading;
using System.Threading.Tasks;

namespace SephiriaOne
{
    internal enum UpdatePhase { Initializing, Idle, Checking, Available, Current, Installing, RestartRequired, Failed }
    internal sealed class UpdateSnapshot
    {
        internal UpdatePhase Phase { get; set; }
        internal Version Running { get; set; }
        internal Version Installed { get; set; }
        internal UpdateRelease Candidate { get; set; }
        internal bool Automatic { get; set; }
        internal string Error { get; set; }
        internal long Revision { get; set; }
        internal long Notice { get; set; }
        internal DateTimeOffset NextCheck { get; set; }
        internal bool Busy => Phase == UpdatePhase.Initializing || Phase == UpdatePhase.Checking || Phase == UpdatePhase.Installing;
        internal bool CanInstall => !Busy && Phase != UpdatePhase.RestartRequired && Candidate != null &&
            Candidate.Version > Running && Candidate.Version > Installed;
    }
    internal sealed class UpdateCoordinator : IDisposable
    {
        private readonly Func<CancellationToken, Task<UpdateRelease>> query;
        private readonly Func<UpdateRelease, CancellationToken, Task> install;
        private readonly Func<Version> installed;
        private readonly Func<DateTimeOffset> clock;
        private readonly UpdateStore store;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private Task<Outcome> work;
        private UpdateCache cache = new UpdateCache();
        private bool disposed, initializing = true;
        private string announced;
        private DateTimeOffset nextManual;
        private DateTimeOffset NextCheck => cache.RetryAfter > nextManual ? cache.RetryAfter : nextManual;
        internal UpdateSnapshot Snapshot { get; private set; }
        internal UpdateCoordinator(Version running, string config, Func<CancellationToken, Task<UpdateRelease>> query,
            Func<UpdateRelease, CancellationToken, Task> install, Func<Version> installed, Func<DateTimeOffset> clock = null)
        {
            this.query = query; this.install = install; this.installed = installed;
            this.clock = clock ?? (() => DateTimeOffset.UtcNow); store = new UpdateStore(config);
            Snapshot = new UpdateSnapshot { Running = running, Installed = running, Phase = UpdatePhase.Initializing, Revision = 1 };
            work = Task.Run(() => new Outcome { Cache = store.Load(), Installed = installed(), Phase = UpdatePhase.Idle });
        }

        // Called only by the local main-thread adapter. Workers return values;
        // they never mutate the published snapshot or call Unity/game APIs.
        internal void Tick()
        {
            if (disposed || work == null || !work.IsCompleted) return;
            Outcome result;
            try { result = work.GetAwaiter().GetResult(); }
            catch (Exception ex) { result = new Outcome { Phase = UpdatePhase.Failed, Error = ex.Message }; }
            work = null;
            if (result.Cache != null) cache = result.Cache;
            bool wasInitializing = initializing; initializing = false;
            if (wasInitializing && result.Error != null) cache.Automatic = false;
            Version disk = result.Installed ?? Snapshot.Installed;
            UpdatePhase phase = result.Phase;
            if (disk > Snapshot.Running) phase = UpdatePhase.RestartRequired;
            else if (phase == UpdatePhase.Idle) phase = cache.Release != null && cache.Release.Version > Snapshot.Running && cache.Release.Version > disk ? UpdatePhase.Available : UpdatePhase.Idle;
            long notice = Snapshot.Notice;
            string available = phase == UpdatePhase.Available ? cache.Release?.Tag : null;
            if ((available != null && announced != available) || result.Notify) notice++;
            if (available != null) announced = available;
            Snapshot = new UpdateSnapshot { Running = Snapshot.Running, Installed = disk, Candidate = cache.Release,
                Automatic = cache.Automatic, Phase = phase, Error = result.Error, Revision = Snapshot.Revision + 1, Notice = notice, NextCheck = NextCheck };
            if (wasInitializing && result.Error == null && phase != UpdatePhase.RestartRequired && cache.Automatic &&
                clock() >= cache.RetryAfter && clock() - cache.LastCheck >= TimeSpan.FromHours(6)) BeginCheck(false);
        }

        internal bool Check() => BeginCheck(true);
        private bool BeginCheck(bool manual)
        {
            if (disposed || Snapshot.Busy || Snapshot.Phase == UpdatePhase.RestartRequired || clock() < cache.RetryAfter || clock() < nextManual) return false;
            var next = cache.Copy(); next.LastCheck = clock(); nextManual = clock().AddSeconds(30);
            Begin(UpdatePhase.Checking, async token =>
            {
                try
                {
                    var release = await query(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    if (release != null) UpdateRelease.Validate(release);
                    next.Release = release; next.RetryAfter = default;
                    store.Save(next);
                    var disk = installed();
                    return new Outcome { Cache = next, Installed = disk, Notify = manual,
                        Phase = release != null && release.Version > Snapshot.Running && release.Version > disk ? UpdatePhase.Available : UpdatePhase.Current };
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    next.RetryAfter = ex is UpdateRateLimitException limited ? limited.RetryAfter : clock().AddMinutes(1);
                    // Cache failure must not discard the actual check error.
                    try { store.Save(next); } catch { }
                    return new Outcome { Cache = next, Phase = UpdatePhase.Failed, Error = ex.Message, Notify = manual };
                }
            });
            return true;
        }

        internal bool Install()
        {
            if (disposed || !Snapshot.CanInstall || clock() < cache.RetryAfter) return false;
            UpdateRelease selected = Snapshot.Candidate;
            Begin(UpdatePhase.Installing, async token =>
            {
                // Consent applies to the displayed release, never a different
                // version/asset that appeared while the menu was open.
                var fresh = await query(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (fresh == null || fresh.Version != selected.Version || fresh.Tag != selected.Tag ||
                    fresh.Sha256 != selected.Sha256 || fresh.DownloadUrl != selected.DownloadUrl || fresh.Size != selected.Size)
                    throw new InvalidOperationException("The selected release changed. Check for updates again before installing.");
                await install(selected, token).ConfigureAwait(false);
                // The installer has committed. Do not convert success into a
                // cancelled/failed outcome after the atomic activation boundary.
                return new Outcome { Installed = selected.Version, Phase = UpdatePhase.RestartRequired, Notify = true };
            });
            return true;
        }

        internal bool SetAutomatic(bool enabled)
        {
            if (disposed || Snapshot.Busy) return false;
            var next = cache.Copy(); next.Automatic = enabled;
            var phase = Snapshot.Phase;
            Begin(UpdatePhase.Initializing, token =>
            {
                token.ThrowIfCancellationRequested(); store.Save(next);
                return Task.FromResult(new Outcome { Cache = next, Phase = phase == UpdatePhase.Failed ? UpdatePhase.Idle : phase });
            });
            return true;
        }

        private void Begin(UpdatePhase phase, Func<CancellationToken, Task<Outcome>> operation)
        {
            Snapshot = new UpdateSnapshot { Running = Snapshot.Running, Installed = Snapshot.Installed,
                Candidate = Snapshot.Candidate, Automatic = cache.Automatic, Phase = phase,
                Revision = Snapshot.Revision + 1, Notice = Snapshot.Notice, NextCheck = NextCheck };
            work = Task.Run(async () =>
            {
                try { return await operation(cancellation.Token).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    var next = cache.Copy();
                    if (ex is UpdateRateLimitException limited)
                    { next.RetryAfter = limited.RetryAfter; try { store.Save(next); } catch { } }
                    return new Outcome { Cache = next, Phase = UpdatePhase.Failed, Error = ex.Message, Notify = true };
                }
            });
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; cancellation.Cancel();
            if (work != null) work.ContinueWith(task => { var ignored = task.Exception; cancellation.Dispose(); }, TaskScheduler.Default);
            else cancellation.Dispose();
        }
        private sealed class Outcome
        {
            internal UpdateCache Cache;
            internal Version Installed;
            internal UpdatePhase Phase;
            internal string Error;
            internal bool Notify;
        }
    }
}
