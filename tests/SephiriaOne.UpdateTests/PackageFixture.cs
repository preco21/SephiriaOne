using System.IO.Compression;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using SephiriaOne;

internal static class PackageFixture
{
    internal static void Run(string previousZip, string nextZip)
    {
        string scratch = Path.Combine(Path.GetTempPath(), "SephiriaOne-package-fixture-" + Guid.NewGuid().ToString("N"));
        string root = Path.Combine(scratch, "AddOns", "SephiriaOne");
        Directory.CreateDirectory(root);
        try
        {
            using (var previous = ZipFile.OpenRead(previousZip))
                foreach (string name in new[] { "metadata.json", "SephiriaOne.dll" })
                    previous.GetEntry(name)!.ExtractToFile(Path.Combine(root, name));
            byte[] oldDll = File.ReadAllBytes(Path.Combine(root, "SephiriaOne.dll"));
            string oldMetadata = File.ReadAllText(Path.Combine(root, "metadata.json"));
            string duplicate = Path.Combine(scratch, "AddOns", "SecondCopy");
            Directory.CreateDirectory(duplicate);
            File.WriteAllBytes(Path.Combine(duplicate, "SephiriaOne.dll"), oldDll);
            File.WriteAllText(Path.Combine(duplicate, "metadata.json"), oldMetadata);
            using var next = ZipFile.OpenRead(nextZip);
            using var reader = new StreamReader(next.GetEntry("metadata.json")!.Open());
            var version = Version.Parse(JObject.Parse(reader.ReadToEnd())["modVersion"]!.Value<string>()!);
            byte[] bytes = File.ReadAllBytes(nextZip);
            var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var release = new UpdateRelease(version, "v" + version,
                "https://github.com/preco21/SephiriaOne/releases/download/v" + version + "/SephiriaOne.zip", digest, bytes.LongLength);
            var installer = new UpdateInstaller(root, Path.Combine(root, "SephiriaOne.dll"));
            installer.Install(release, bytes, CancellationToken.None);
            if (installer.ReadInstalledVersion() != version ||
                !File.ReadAllBytes(Path.Combine(root, "SephiriaOne.dll")).SequenceEqual(oldDll) ||
                !Directory.GetFiles(root, "*.previous.json").Any(path => File.ReadAllText(path) == oldMetadata) ||
                !File.Exists(Path.Combine(root, "SephiriaOne." + version + ".dll")) ||
                File.ReadAllText(Path.Combine(duplicate, "metadata.json")) != oldMetadata ||
                !File.ReadAllBytes(Path.Combine(duplicate, "SephiriaOne.dll")).SequenceEqual(oldDll))
                throw new Exception("Real release package transaction failed");
            Console.WriteLine($"Verified packaged {version} installs over previous release in a temporary fixture; original DLL/metadata backup preserved. No game files touched.");
        }
        finally { Directory.Delete(scratch, true); }
    }
}
