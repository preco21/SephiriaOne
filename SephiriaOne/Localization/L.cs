#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace SephiriaOne
{
    internal static class L
    {
        private const int MaxFileBytes = 1024 * 1024;
        private const int MaxEntries = 4096;
        private const int MaxTextLength = 16384;
        private const int MaxArguments = 64;
        private const int MaxAlignment = 1024;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static Snapshot current = Snapshot.Empty;
        private static Snapshot bundled = Snapshot.Empty;
        private static string directory;
        private static Action<string> warning;

        internal static string Language => current.Language;
        internal static string ConfigPath => directory == null ? string.Empty : Path.Combine(directory, "config.json");
        internal static int Revision { get; private set; }
        internal static event Action Changed;

        // Only explicit source strings are lookup keys. Formatted arguments and user text
        // never pass through a replacement or translation step.
        internal static string T(string english)
        {
            if (english == null) return string.Empty;
            Snapshot snapshot = current;
            if (snapshot.Language == "ko" && snapshot.Korean.TryGetValue(english, out string korean))
                return korean;
            return English(snapshot, english);
        }

        internal static string F(string english, params object[] args)
        {
            english = english ?? string.Empty;
            args = args ?? Array.Empty<object>();
            try { return string.Format(CultureInfo.InvariantCulture, T(english), args); }
            catch (FormatException)
            {
                try { return string.Format(CultureInfo.InvariantCulture, English(current, english), args); }
                catch (FormatException) { return english; }
            }
        }

        // Some diagnostic contexts append a more specific outcome. Only remove
        // the translated trailing notice; arbitrary catalog edits cannot throw.
        internal static string TrimSuffix(string message, string englishSuffix)
        {
            string suffix = T(englishSuffix);
            return suffix.Length > 0 && message.EndsWith(suffix, StringComparison.Ordinal) ?
                message.Substring(0, message.Length - suffix.Length) : message;
        }

        internal static void Initialize(string folder, Action<string> warn)
        {
            warning = warn;
            directory = null;
            current = bundled = Snapshot.Empty;
            try
            {
                string englishDefault = ReadEmbedded("en");
                string koreanDefault = ReadEmbedded("ko");
                int ignored = 0;
                current = bundled = new Snapshot("en", ValidateCatalog(ReadObject(englishDefault), ref ignored),
                    ValidateCatalog(ReadObject(koreanDefault), ref ignored));
                directory = Path.GetFullPath(folder);
                Directory.CreateDirectory(directory);
                Seed("en.json", englishDefault);
                Seed("ko.json", koreanDefault);
                Seed("config.json", "{\"language\":\"ko\"}\n");
                Snapshot loaded = Load(out ignored);
                Publish(loaded);
                WarnIgnored(ignored);
            }
            catch (Exception ex)
            {
                Publish(new Snapshot("en", current.English, current.Korean));
                Warn(Failure("initialize", ex));
            }
        }

        internal static bool TrySetLanguage(string language, out string error)
        {
            error = null;
            if (!Supported(language))
            {
                error = T("Language must be en or ko.");
                return false;
            }
            if (directory == null)
            {
                error = T("Localization is not initialized.");
                return false;
            }
            try
            {
                // Persist even an unchanged selection, so an edited config can be repaired.
                WriteAtomic("config.json", "{\"language\":\"" + language + "\"}\n", true);
                if (current.Language != language)
                    Publish(new Snapshot(language, current.English, current.Korean));
                return true;
            }
            catch (Exception ex) { error = Failure("save language", ex); return false; }
        }

        internal static bool Reload(out string error)
        {
            error = null;
            if (directory == null)
            {
                error = T("Localization is not initialized.");
                return false;
            }
            try
            {
                Snapshot loaded = Load(out int ignored);
                Publish(loaded);
                WarnIgnored(ignored);
                return true;
            }
            catch (Exception ex) { error = Failure("reload", ex); return false; }
        }

        internal static void Shutdown()
        {
            Changed = null;
            warning = null;
            directory = null;
            current = bundled = Snapshot.Empty;
            Revision++;
        }

        private static string English(Snapshot snapshot, string english) =>
            snapshot.English.TryGetValue(english, out string translated) ? translated : english;

        private static bool Supported(string language) => language == "en" || language == "ko";

        private static Snapshot Load(out int ignored)
        {
            Dictionary<string, string> config = ReadObject(ReadFile("config.json"));
            if (config.Count != 1 || !config.TryGetValue("language", out string language) || !Supported(language))
                throw new InvalidDataException("config.json must contain a language of en or ko.");
            ignored = 0;
            var english = LoadCatalog("en.json", bundled.English, ref ignored);
            var korean = LoadCatalog("ko.json", bundled.Korean, ref ignored);
            return new Snapshot(language, english, korean);
        }

        private static Dictionary<string, string> LoadCatalog(string file, Dictionary<string, string> defaults, ref int ignored)
        {
            var entries = ReadObject(ReadFile(file));
            var result = ValidateCatalog(entries, ref ignored);
            // Older/custom catalogs are sparse overrides. Fill only absent
            // keys from this DLL's defaults, never a prior edited snapshot.
            // Explicit invalid overrides retain the documented English fallback.
            foreach (var entry in defaults)
                if (!entries.ContainsKey(entry.Key)) result.Add(entry.Key, entry.Value);
            return result;
        }

        private static Dictionary<string, string> ValidateCatalog(Dictionary<string, string> entries, ref int ignored)
        {
            var valid = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Value) || !FormatArguments(entry.Key, out ulong sourceArguments) ||
                    !FormatArguments(entry.Value, out ulong translatedArguments) || sourceArguments != translatedArguments)
                {
                    ignored++;
                    continue;
                }
                valid.Add(entry.Key, entry.Value);
            }
            return valid;
        }

        // Parse the composite-format grammar without formatting attacker-controlled
        // widths or allocating an array sized by a catalog's argument indices.
        private static bool FormatArguments(string format, out ulong arguments)
        {
            arguments = 0;
            for (int index = 0; index < format.Length; index++)
            {
                char ch = format[index];
                if (ch != '{' && ch != '}') continue;
                if (index + 1 < format.Length && format[index + 1] == ch) { index++; continue; }
                if (ch == '}') return false;
                index++;
                if (!Number(format, ref index, MaxArguments - 1, out int argument)) return false;
                arguments |= 1UL << argument;
                Spaces(format, ref index);
                if (index < format.Length && format[index] == ',')
                {
                    index++;
                    Spaces(format, ref index);
                    if (index < format.Length && format[index] == '-') index++;
                    if (!Number(format, ref index, MaxAlignment, out _)) return false;
                    Spaces(format, ref index);
                }
                if (index < format.Length && format[index] == ':')
                {
                    index++;
                    while (index < format.Length && format[index] != '}')
                    {
                        if (format[index] == '{') return false;
                        index++;
                    }
                }
                if (index >= format.Length || format[index] != '}') return false;
            }
            return true;
        }

        private static bool Number(string text, ref int index, int maximum, out int number)
        {
            number = 0;
            int start = index;
            while (index < text.Length && text[index] >= '0' && text[index] <= '9')
            {
                number = number * 10 + text[index++] - '0';
                if (number > maximum) return false;
            }
            return index > start;
        }

        private static void Spaces(string text, ref int index)
        {
            while (index < text.Length && text[index] == ' ') index++;
        }

        private static Dictionary<string, string> ReadObject(string json)
        {
            CheckJsonSyntax(json);
            var entries = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                reader.MaxDepth = 4;
                reader.DateParseHandling = DateParseHandling.None;
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject)
                    throw new InvalidDataException("Localization JSON must be an object.");
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName || reader.QuoteChar != '"')
                        throw new InvalidDataException("Localization keys must be quoted strings.");
                    string key = (string)reader.Value;
                    if (key.Length > MaxTextLength || entries.Count >= MaxEntries)
                        throw new InvalidDataException("Localization catalog exceeds its entry or text limit.");
                    if (entries.ContainsKey(key)) throw new InvalidDataException("Duplicate localization key.");
                    if (!reader.Read() || reader.TokenType != JsonToken.String || reader.QuoteChar != '"')
                        throw new InvalidDataException("Localization values must be strings.");
                    string value = (string)reader.Value;
                    if (value.Length > MaxTextLength) throw new InvalidDataException("Localization text exceeds its length limit.");
                    entries.Add(key, value);
                }
                if (reader.TokenType != JsonToken.EndObject || reader.Read())
                    throw new InvalidDataException("Unexpected content after localization JSON.");
            }
            return entries;
        }

        // Json.NET deliberately accepts JavaScript extensions. Catalogs are JSON,
        // so reject its trailing commas, literal string controls and extra escapes.
        private static void CheckJsonSyntax(string json)
        {
            bool quoted = false;
            char previous = '\0';
            for (int index = 0; index < json.Length; index++)
            {
                char ch = json[index];
                if (quoted)
                {
                    if (ch < ' ') throw new InvalidDataException("Unescaped control character in localization JSON.");
                    if (ch == '"') { quoted = false; previous = '"'; }
                    else if (ch == '\\')
                    {
                        if (++index >= json.Length || "\"\\/bfnrtu".IndexOf(json[index]) < 0)
                            throw new InvalidDataException("Invalid escape in localization JSON.");
                    }
                    continue;
                }
                if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n') continue;
                if (ch == '"') quoted = true;
                else if (ch == '}' && previous == ',')
                    throw new InvalidDataException("Trailing comma in localization JSON.");
                else if (ch != '{' && ch != '}' && ch != ':' && ch != ',')
                    throw new InvalidDataException("Localization JSON permits only string properties.");
                previous = ch;
            }
        }

        private static string ReadFile(string file)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, file))) return ReadBounded(stream);
        }

        private static string ReadEmbedded(string language)
        {
            using (Stream stream = typeof(L).Assembly.GetManifestResourceStream("SephiriaOne.Localization." + language + ".json"))
            {
                if (stream == null) throw new InvalidDataException("Embedded localization catalog is missing.");
                return ReadBounded(stream);
            }
        }

        private static string ReadBounded(Stream stream)
        {
            using (var bytes = new MemoryStream())
            {
                var buffer = new byte[4096];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (bytes.Length + read > MaxFileBytes) throw new InvalidDataException("Localization file exceeds 1 MiB.");
                    bytes.Write(buffer, 0, read);
                }
                bytes.Position = 0;
                using (var reader = new StreamReader(bytes, Utf8, true)) return reader.ReadToEnd();
            }
        }

        private static void Seed(string file, string content)
        {
            if (!File.Exists(Path.Combine(directory, file))) WriteAtomic(file, content, false);
        }

        private static void WriteAtomic(string file, string content, bool replace)
        {
            string target = Path.Combine(directory, file);
            string temporary = Path.Combine(directory, ".localization-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Utf8.GetBytes(content);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (replace && File.Exists(target)) File.Replace(temporary, target, null);
                else
                {
                    try { File.Move(temporary, target); }
                    catch (IOException) when (!replace && File.Exists(target)) { /* Another initializer seeded it. */ }
                }
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static void Publish(Snapshot snapshot)
        {
            current = snapshot;
            Revision++;
            Action handlers = Changed;
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception ex) { Warn(Failure("refresh localization view", ex)); }
            }
        }

        private static string Failure(string operation, Exception ex)
        {
            string message = ex.Message.Replace('\r', ' ').Replace('\n', ' ');
            if (message.Length > 320) message = message.Substring(0, 320);
            return "Could not " + operation + " localization (" + ex.GetType().Name + "): " + message;
        }

        private static void WarnIgnored(int ignored)
        {
            if (ignored > 0) Warn("Ignored " + ignored.ToString(CultureInfo.InvariantCulture) +
                " localization entries with blank text or invalid format placeholders; English fallback is active for those entries.");
        }

        private static void Warn(string message)
        {
            try { warning?.Invoke(message); }
            catch (Exception) { /* Logging must not prevent safe fallback. */ }
        }

        private sealed class Snapshot
        {
            internal static readonly Snapshot Empty = new Snapshot("en",
                new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));
            internal readonly string Language;
            internal readonly Dictionary<string, string> English;
            internal readonly Dictionary<string, string> Korean;

            internal Snapshot(string language, Dictionary<string, string> english, Dictionary<string, string> korean)
            {
                Language = language;
                English = english;
                Korean = korean;
            }
        }
    }
}
