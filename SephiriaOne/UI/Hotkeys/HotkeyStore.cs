using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
namespace SephiriaOne
{
    internal sealed class HotkeyStore
    {
        private readonly string path;
        internal HotkeyStore(string path) { this.path = path; }
        internal string Load(Action<string> warning)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > 4096) throw new IOException("UI preference too large.");
                var json = JObject.Parse(File.ReadAllText(path));
                if (json["version"]?.Type != JTokenType.Integer || (int)json["version"] != 1 ||
                    (json["key"]?.Type != JTokenType.Null && json["key"]?.Type != JTokenType.String))
                    throw new FormatException("Invalid UI preference schema.");
                return (string)json["key"];
            }
            catch (Exception exception) { warning(exception.Message); return null; }
        }
        internal bool TrySave(string key, out string error)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                byte[] bytes = new UTF8Encoding(false).GetBytes(new JObject { ["version"] = 1, ["key"] = key == null ? JValue.CreateNull() : new JValue(key) }.ToString());
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
                error = ""; return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
