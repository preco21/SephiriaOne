using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SephiriaOne
{
    internal sealed class UpdateCache
    {
        public bool Automatic { get; set; } = true;
        public DateTimeOffset LastCheck { get; set; }
        public DateTimeOffset RetryAfter { get; set; }
        public UpdateRelease Release { get; set; }
        internal UpdateCache Copy() => new UpdateCache { Automatic = Automatic, LastCheck = LastCheck, RetryAfter = RetryAfter, Release = Release };
    }

    internal sealed class UpdateStore
    {
        private readonly string path;
        internal UpdateStore(string path) { this.path = path; }
        internal UpdateCache Load()
        {
            if (!File.Exists(path)) return new UpdateCache();
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Update cache exceeds its size limit.");
            var json = GitHubReleaseClient.ParseObject(File.ReadAllText(path));
            // Json.NET binds DTO members without regard to case and coerces
            // string Booleans. Reject aliases before that binding can override
            // a validated explicit opt-out.
            RequireNames(json, "Automatic", "LastCheck", "RetryAfter", "Release");
            if (json["Automatic"] == null || json["Automatic"].Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                throw new InvalidDataException("Automatic must be a JSON Boolean.");
            if (json["Release"] is Newtonsoft.Json.Linq.JObject release)
                RequireNames(release, "Version", "Tag", "DownloadUrl", "Sha256", "Size");
            var cache = json.ToObject<UpdateCache>();
            if (cache == null) throw new InvalidDataException("Update cache is empty.");
            if (cache.Release != null) UpdateRelease.Validate(cache.Release);
            return cache;
        }
        private static void RequireNames(Newtonsoft.Json.Linq.JObject json, params string[] names)
        {
            var allowed = new HashSet<string>(names, StringComparer.Ordinal);
            foreach (var property in json.Properties())
                if (!allowed.Contains(property.Name)) throw new InvalidDataException("Unrecognized or incorrectly cased update cache field.");
        }
        internal void Save(UpdateCache cache)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(cache, Formatting.Indented));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
