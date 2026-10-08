using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace SephiriaOne
{
    internal sealed class UpdateInstaller
    {
        private const int MaximumMetadataBytes = 64 * 1024;
        private const int MaximumDllBytes = 32 * 1024 * 1024;
        private readonly string folder;
        private readonly string runningDll;
        private readonly string metadataPath;
        public UpdateInstaller(string addonFolder, string runningDllPath)
        {
            if (string.IsNullOrWhiteSpace(addonFolder) || string.IsNullOrWhiteSpace(runningDllPath)) throw new InvalidDataException("Cannot locate the addon installation.");
            folder = Path.GetFullPath(addonFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            runningDll = Path.GetFullPath(runningDllPath);
            if (!string.Equals(Path.GetDirectoryName(runningDll), folder, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Running DLL must be inside the addon folder.");
            ValidateFolder();
            RequireRegularFile(runningDll);
            metadataPath = Path.Combine(folder, "metadata.json");
        }
        public Version ReadInstalledVersion()
        {
            ValidateFolder();
            return ReadMetadata(ReadFile(metadataPath, MaximumMetadataBytes), false).Version;
        }
        public void Install(UpdateRelease release, byte[] archive, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            release.VerifyArchive(archive);
            ValidateFolder();
            var lockPath = Path.Combine(folder, ".one-update.lock");
            RejectReparseIfPresent(lockPath);
            // FileShare.None serializes updater transactions across processes. The empty lock file
            // remains in this addon directory and cannot be discovered as another addon.
            using (var transaction = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var originalBytes = ReadFile(metadataPath, MaximumMetadataBytes);
                var installed = ReadMetadata(originalBytes, false);
                if (release.Version <= installed.Version) throw new InvalidOperationException("This version is already installed or is older than the installed version.");
                byte[] dllBytes;
                PackageMetadata package;
                using (var input = new MemoryStream(archive, false))
                using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    if (zip.Entries.Count != 2) throw new InvalidDataException("Update ZIP must contain only metadata.json and SephiriaOne.dll at its root.");
                    var entries = zip.Entries;
                    foreach (var entry in entries)
                    {
                        if (!(entry.FullName == "metadata.json" || entry.FullName == "SephiriaOne.dll")) throw new InvalidDataException("Unexpected ZIP entry: " + entry.FullName + ".");
                        var unixType = (entry.ExternalAttributes >> 16) & 0xf000;
                        if ((unixType != 0 && unixType != 0x8000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                            throw new InvalidDataException("ZIP entries must be regular files.");
                    }
                    if (entries.Count(e => e.FullName == "metadata.json") != 1 || entries.Count(e => e.FullName == "SephiriaOne.dll") != 1)
                        throw new InvalidDataException("Duplicate or missing ZIP entries.");
                    package = ReadMetadata(ReadEntry(entries.Single(e => e.FullName == "metadata.json"), MaximumMetadataBytes, cancellationToken), true);
                    if (package.Version != release.Version) throw new InvalidDataException("Package metadata version does not match the release tag.");
                    dllBytes = ReadEntry(entries.Single(e => e.FullName == "SephiriaOne.dll"), MaximumDllBytes, cancellationToken);
                }
                var name = "SephiriaOne." + release.Version.ToString(3) + ".dll";
                var destination = Path.Combine(folder, name);
                if (string.Equals(destination, runningDll, StringComparison.OrdinalIgnoreCase) || File.Exists(destination) || Directory.Exists(destination))
                    throw new IOException("The version DLL already exists; it will not be overwritten. Review the installation manually.");
                var id = Guid.NewGuid().ToString("N");
                var stageDll = Path.Combine(folder, ".one-update-" + id + ".dll");
                var stageMetadata = Path.Combine(folder, ".one-update-" + id + ".json");
                var backup = Path.Combine(folder, ".one-update-" + id + ".previous.json");
                try
                {
                    WriteNewFile(stageDll, dllBytes, cancellationToken);
                    ValidateAssembly(stageDll, release.Version);
                    ValidateFolder();
                    cancellationToken.ThrowIfCancellationRequested();
                    File.Move(stageDll, destination);
                    RequireRegularFile(destination);
                    ValidateAssembly(destination, release.Version);
                    package.Json["dllFile"] = name;
                    var nextMetadata = new UTF8Encoding(false, true).GetBytes(package.Json.ToString(Formatting.Indented));
                    if (nextMetadata.Length > MaximumMetadataBytes) throw new InvalidDataException("Installed metadata exceeds the allowed size.");
                    WriteNewFile(stageMetadata, nextMetadata, cancellationToken);
                    ValidateFolder();
                    RequireRegularFile(destination);
                    if (!originalBytes.SequenceEqual(ReadFile(metadataPath, MaximumMetadataBytes))) throw new IOException("Installed metadata changed during the update; retry after reviewing the installation.");
                    RejectReparseIfPresent(backup);
                    cancellationToken.ThrowIfCancellationRequested();
                    // This is the only activation point. Once it succeeds, cancellation must not
                    // turn a committed installation into a reported failure.
                    File.Replace(stageMetadata, metadataPath, backup);
                }
                finally
                {
                    TryDelete(stageDll);
                    TryDelete(stageMetadata);
                    // Retain every completed immutable destination. A concurrent manual metadata
                    // change can select it before our commit, so deleting it on failure could
                    // break the active pointer. Only temporary staging files are safe to remove.
                }
            }
        }
        private void ValidateFolder()
        {
            var directory = new DirectoryInfo(folder);
            if (!directory.Exists) throw new DirectoryNotFoundException("Addon installation folder does not exist.");
            for (var current = directory; current != null; current = current.Parent)
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Updater cannot use linked or redirected installation folders.");
        }
        private PackageMetadata ReadMetadata(byte[] bytes, bool package)
        {
            var json = GitHubReleaseClient.ParseObject(new UTF8Encoding(false, true).GetString(bytes));
            if (GitHubReleaseClient.Required(json, "modName", JTokenType.String).Value<string>() != "SephiriaOne" ||
                GitHubReleaseClient.Required(json, "entryClass", JTokenType.String).Value<string>() != "SephiriaOne.Entry")
                throw new InvalidDataException("Metadata does not identify SephiriaOne.Entry.");
            var text = GitHubReleaseClient.Required(json, "modVersion", JTokenType.String).Value<string>();
            if (!UpdateRelease.TryParseVersion(text, out var version)) throw new InvalidDataException("Metadata version must contain three components.");
            var dll = GitHubReleaseClient.Required(json, "dllFile", JTokenType.String).Value<string>();
            if (dll != "SephiriaOne.dll" && (package || dll != "SephiriaOne." + version.ToString(3) + ".dll"))
                throw new InvalidDataException("Metadata must reference the expected addon DLL.");
            if (!package) RequireRegularFile(Path.Combine(folder, dll));
            return new PackageMetadata(json, version);
        }
        private static byte[] ReadEntry(ZipArchiveEntry entry, int maximum, CancellationToken token)
        {
            if (entry.Length <= 0 || entry.Length > maximum) throw new InvalidDataException("Expanded ZIP entry exceeds its allowed size.");
            using (var input = entry.Open())
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var read = input.Read(buffer, 0, Math.Min(buffer.Length, maximum - (int)output.Length + 1));
                    if (read == 0) break;
                    if (output.Length + read > maximum) throw new InvalidDataException("Expanded ZIP entry exceeds its allowed size.");
                    output.Write(buffer, 0, read);
                }
                if (output.Length != entry.Length) throw new InvalidDataException("Expanded ZIP entry length does not match the archive.");
                return output.ToArray();
            }
        }
        private static byte[] ReadFile(string path, int maximum)
        {
            RequireRegularFile(path);
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length <= 0 || input.Length > maximum) throw new InvalidDataException("Metadata exceeds its allowed size.");
                var bytes = new byte[(int)input.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var next = input.Read(bytes, read, bytes.Length - read);
                    if (next == 0) throw new EndOfStreamException("Metadata changed while being read.");
                    read += next;
                }
                return bytes;
            }
        }
        private static void WriteNewFile(string path, byte[] bytes, CancellationToken token)
        {
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                for (var offset = 0; offset < bytes.Length; offset += 8192)
                {
                    token.ThrowIfCancellationRequested();
                    file.Write(bytes, offset, Math.Min(8192, bytes.Length - offset));
                }
                file.Flush(true);
            }
        }
        private static void ValidateAssembly(string path, Version expected)
        {
            var assembly = AssemblyName.GetAssemblyName(path);
            var version = assembly.Version;
            if (assembly.Name != "SephiriaOne" || version == null || version.Revision > 0 || new Version(version.Major, version.Minor, version.Build) != expected)
                throw new InvalidDataException("Downloaded DLL identity/version does not match the release.");
        }
        private static void RequireRegularFile(string path)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new IOException("Updater requires regular files in the installation folder.");
        }
        private static void RejectReparseIfPresent(string path)
        {
            try { RequireRegularFile(path); } catch (FileNotFoundException) { }
        }
        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) { RequireRegularFile(path); File.Delete(path); } } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private sealed class PackageMetadata
        {
            public JObject Json { get; }
            public Version Version { get; }
            public PackageMetadata(JObject json, Version version) { Json = json; Version = version; }
        }
    }
}
