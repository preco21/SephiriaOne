#nullable enable
using System;
using System.IO;
using System.Text;

namespace SephiriaOne
{
    internal sealed class PresetStore
    {
        public string FilePath { get; }
        public PresetStore(string filePath) => FilePath = filePath;

        public bool TryLoad(out SessionPolicy policy, out bool exists, out string error)
        {
            policy = new SessionPolicy();
            exists = false;
            error = "";
            try
            {
                using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                exists = true;
                if (stream.Length > SessionPolicy.MaximumPresetLength)
                {
                    error = L.T("Saved preset is too large; no saved settings were applied.");
                    return false;
                }
                using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
                return SessionPolicy.TryReadPreset(reader.ReadToEnd(), out policy, out error);
            }
            catch (FileNotFoundException) { return true; }
            catch (DirectoryNotFoundException) { return true; }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is DecoderFallbackException)
            {
                error = L.T("Could not read saved preset: ") + exception.Message;
                return false;
            }
        }

        public bool TrySave(SessionPolicy policy, out string error)
        {
            string text = policy.ToPresetText();
            if (!SessionPolicy.TryReadPreset(text, out _, out error)) return false;
            string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
                else File.Move(temporary, FilePath);
                error = "";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = L.T("Could not save preset; previous saved settings were kept: ") + exception.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        public bool TryForget(out string error)
        {
            try
            {
                File.Delete(FilePath);
                error = "";
                return true;
            }
            catch (DirectoryNotFoundException) { error = ""; return true; }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = L.T("Could not remove saved preset: ") + exception.Message;
                return false;
            }
        }
    }
}
