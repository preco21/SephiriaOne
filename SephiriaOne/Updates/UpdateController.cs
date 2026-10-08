using System;
using System.IO;
using UnityEngine;

namespace SephiriaOne
{
    // Installation-local lifecycle; never participates in player reconciliation.
    public sealed class UpdateController : MonoBehaviour
    {
        private GitHubReleaseClient client;
        private UpdateCoordinator service;
        private float nextTick;
        private long noticed, logged;
        private long noticeFailure;

        private void OnEnable()
        {
            noticed = logged = noticeFailure = 0; nextTick = 0;
            try
            {
                string dll = typeof(Entry).Assembly.Location;
                string folder = Path.GetDirectoryName(Path.GetFullPath(dll));
                string root = Path.GetFullPath(AddOnLoader.AddOnsPath).TrimEnd(Path.DirectorySeparatorChar);
                if (!string.Equals(Path.GetDirectoryName(folder), root, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Updater requires an installation directly under the game's AddOns folder.");
                var installer = new UpdateInstaller(folder, dll);
                var version = typeof(Entry).Assembly.GetName().Version;
                client = new GitHubReleaseClient();
                service = new UpdateCoordinator(new Version(version.Major, version.Minor, version.Build),
                    Path.Combine(Application.persistentDataPath, "SephiriaOne", "updates.json"), client.CheckAsync,
                    async (release, token) =>
                    {
                        byte[] archive = await client.DownloadAsync(release, token).ConfigureAwait(false);
                        installer.Install(release, archive, token);
                    }, installer.ReadInstalledVersion);
                UpdateFeature.Bind(service);
            }
            catch (Exception exception)
            {
                client?.Dispose(); client = null;
                Debug.LogWarning("[SephiriaOne] Updater unavailable: " + exception);
            }
        }

        private void Update()
        {
            if (service == null || Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.25f;
            service.Tick();
            var state = service.Snapshot;
            if (state.Error != null && logged != state.Revision)
            { logged = state.Revision; Debug.LogWarning("[SephiriaOne] Update operation failed: " + state.Error); }
            // Keep the notice queued through title/loading scenes. WriteLog is
            // local; no chat RPC or update data is ever sent to stock guests.
            if (!state.Busy && state.Notice != noticed && GameLogWriter.Instance)
            {
                try
                {
                    GameLogWriter.Instance.WriteLog("[SephiriaOne] " + UpdateText.Summary(state), Color.yellow);
                    noticed = state.Notice;
                }
                catch (Exception exception)
                {
                    nextTick = Time.unscaledTime + 10;
                    if (noticeFailure != state.Notice)
                    { noticeFailure = state.Notice; Debug.LogWarning("[SephiriaOne] Update notice delayed: " + exception.Message); }
                }
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(UpdateFeature.Service, service)) UpdateFeature.Stop();
            else service?.Dispose();
            service = null; client?.Dispose(); client = null;
        }
    }
}
