using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace SephiriaOne
{
    internal sealed class UpdateRelease
    {
        public Version Version { get; }
        public string Tag { get; }
        public string DownloadUrl { get; }
        public string Sha256 { get; }
        public long Size { get; }
        public const int MaximumArchiveBytes = 16 * 1024 * 1024;
        public UpdateRelease(Version version, string tag, string downloadUrl, string sha256, long size)
        {
            if (version == null || version.Build < 0 || version.Revision > 0) throw new InvalidDataException("Release version must contain three components.");
            Version = new Version(version.Major, version.Minor, version.Build);
            Tag = tag;
            DownloadUrl = downloadUrl;
            Sha256 = sha256?.ToLowerInvariant();
            Size = size;
            Validate(this);
        }
        public static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            return text != null && Regex.IsMatch(text, @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z") && Version.TryParse(text, out version);
        }
        public static void Validate(UpdateRelease release)
        {
            if (release == null || release.Tag == null || !release.Tag.StartsWith("v", StringComparison.Ordinal) ||
                !TryParseVersion(release.Tag.Substring(1), out var tagged) || tagged != release.Version)
                throw new InvalidDataException("Release tag must be a stable vX.Y.Z version matching the DLL.");
            var expected = "https://github.com/preco21/SephiriaOne/releases/download/" + release.Tag + "/SephiriaOne.zip";
            if (!string.Equals(release.DownloadUrl, expected, StringComparison.Ordinal)) throw new InvalidDataException("Release asset must belong to preco21/SephiriaOne on GitHub.");
            if (release.Sha256 == null || !Regex.IsMatch(release.Sha256, @"\A[a-f0-9]{64}\z")) throw new InvalidDataException("A SHA-256 release asset digest is required.");
            if (release.Size <= 0 || release.Size > MaximumArchiveBytes) throw new InvalidDataException("Release archive size is outside the supported limit.");
        }
        internal void VerifyArchive(byte[] bytes)
        {
            Validate(this);
            if (bytes == null || bytes.LongLength != Size) throw new InvalidDataException("Downloaded archive size does not match the release.");
            using (var hash = SHA256.Create())
            {
                var actual = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, Sha256, StringComparison.Ordinal)) throw new InvalidDataException("Downloaded archive SHA-256 does not match the release.");
            }
        }
    }
}
