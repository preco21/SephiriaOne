using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace SephiriaOne
{
    internal sealed class GitHubReleaseClient : IDisposable
    {
        private const string LatestUrl = "https://api.github.com/repos/preco21/SephiriaOne/releases/latest";
        private readonly HttpClient client;
        private string etag;
        private UpdateRelease cachedRelease;
        private bool hasCachedResponse;
        public GitHubReleaseClient(HttpMessageHandler handler = null)
        {
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false }, true);
            client.Timeout = TimeSpan.FromSeconds(45);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SephiriaOne-Updater/1.0");
        }
        public async Task<UpdateRelease> CheckAsync(CancellationToken cancellationToken)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(45));
                using (var request = new HttpRequestMessage(HttpMethod.Get, LatestUrl))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
                    if (etag != null) request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            etag = null;
                            cachedRelease = null;
                            hasCachedResponse = false;
                            return null;
                        }
                        if (response.StatusCode == HttpStatusCode.NotModified)
                        {
                            if (!hasCachedResponse) throw new InvalidDataException("GitHub returned an uncached response; retry the update check.");
                            return cachedRelease;
                        }
                        EnsureSuccess(response);
                        var bytes = await ReadBoundedAsync(response, 256 * 1024, timeout.Token).ConfigureAwait(false);
                        var json = ParseObject(new UTF8Encoding(false, true).GetString(bytes));
                        var draft = Required(json, "draft", JTokenType.Boolean).Value<bool>();
                        var prerelease = Required(json, "prerelease", JTokenType.Boolean).Value<bool>();
                        UpdateRelease release = null;
                        if (!draft && !prerelease)
                        {
                            var tag = Required(json, "tag_name", JTokenType.String).Value<string>();
                            if (tag == null || !tag.StartsWith("v", StringComparison.Ordinal) || !UpdateRelease.TryParseVersion(tag.Substring(1), out var version))
                                throw new InvalidDataException("GitHub release must use a stable vX.Y.Z tag.");
                            var assets = (JArray)Required(json, "assets", JTokenType.Array);
                            var matches = assets.OfType<JObject>().Where(a => a["name"]?.Type == JTokenType.String && a["name"].Value<string>() == "SephiriaOne.zip").ToArray();
                            if (matches.Length != 1) throw new InvalidDataException("GitHub release needs exactly one SephiriaOne.zip asset.");
                            var asset = matches[0];
                            var digest = Required(asset, "digest", JTokenType.String).Value<string>();
                            if (digest == null || !digest.StartsWith("sha256:", StringComparison.Ordinal)) throw new InvalidDataException("GitHub asset does not provide a SHA-256 digest.");
                            release = new UpdateRelease(version, tag, Required(asset, "browser_download_url", JTokenType.String).Value<string>(), digest.Substring(7), Required(asset, "size", JTokenType.Integer).Value<long>());
                        }
                        cachedRelease = release;
                        hasCachedResponse = true;
                        etag = response.Headers.ETag?.ToString();
                        return release;
                    }
                }
            }
        }
        public async Task<byte[]> DownloadAsync(UpdateRelease release, CancellationToken cancellationToken)
        {
            UpdateRelease.Validate(release);
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(45));
                var url = new Uri(release.DownloadUrl);
                for (var redirects = 0; redirects <= 4; redirects++)
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                    {
                        var code = (int)response.StatusCode;
                        if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                        {
                            var location = response.Headers.Location;
                            if (location == null || redirects == 4) throw new InvalidDataException("GitHub download redirect limit reached.");
                            var next = location.IsAbsoluteUri ? location : new Uri(url, location);
                            if (next.Scheme != "https" || next.Port != 443 || next.UserInfo.Length != 0 ||
                                !(next.Host == "release-assets.githubusercontent.com" || next.Host == "objects.githubusercontent.com" || next.Host == "github-releases.githubusercontent.com" || next.AbsoluteUri == release.DownloadUrl))
                                throw new InvalidDataException("GitHub redirected the download to an untrusted location.");
                            url = next;
                            continue;
                        }
                        EnsureSuccess(response);
                        var bytes = await ReadBoundedAsync(response, (int)release.Size, timeout.Token).ConfigureAwait(false);
                        timeout.Token.ThrowIfCancellationRequested();
                        release.VerifyArchive(bytes);
                        return bytes;
                    }
                }
            }
            throw new InvalidDataException("GitHub download did not complete.");
        }
        internal static JObject ParseObject(string text)
        {
            ValidateJsonSyntax(text);
            using (var reader = new StrictJsonReader(new StringReader(text)) { MaxDepth = 32, DateParseHandling = DateParseHandling.None })
            {
                var parsed = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error, CommentHandling = CommentHandling.Load });
                if (reader.Read() || parsed.Descendants().Any(t => t.Type == JTokenType.Comment)) throw new InvalidDataException("Unexpected data in JSON document.");
                return parsed;
            }
        }
        private sealed class StrictJsonReader : JsonTextReader
        {
            public StrictJsonReader(TextReader input) : base(input) { }
            public override bool Read()
            {
                var result = base.Read();
                if (result && (TokenType == JsonToken.PropertyName || TokenType == JsonToken.String) && QuoteChar != '"')
                    throw new InvalidDataException("JSON names and strings require double quotes.");
                return result;
            }
        }
        // Newtonsoft accepts JavaScript extensions (comments, single quotes, NaN,
        // unquoted names and trailing commas). Release/package records require JSON.
        private static void ValidateJsonSyntax(string text)
        {
            var previous = '\0';
            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (current == ' ' || current == '\t' || current == '\r' || current == '\n') continue;
                if (current == '"')
                {
                    var closed = false;
                    while (++index < text.Length)
                    {
                        current = text[index];
                        if (current == '"') { closed = true; break; }
                        if (current < 32) throw new InvalidDataException("JSON strings cannot contain unescaped control characters.");
                        if (current != '\\') continue;
                        if (++index == text.Length) throw new InvalidDataException("Incomplete JSON escape.");
                        current = text[index];
                        if (current == 'u')
                        {
                            for (var hex = 0; hex < 4; hex++)
                                if (++index == text.Length || !Uri.IsHexDigit(text[index])) throw new InvalidDataException("Invalid JSON Unicode escape.");
                        }
                        else if ("\"\\/bfnrt".IndexOf(current) < 0) throw new InvalidDataException("Invalid JSON string escape.");
                    }
                    if (!closed) throw new InvalidDataException("Unterminated JSON string.");
                    previous = '"';
                    continue;
                }
                if ("{}[],:".IndexOf(current) >= 0)
                {
                    if ((current == '}' || current == ']') && previous == ',') throw new InvalidDataException("JSON trailing commas are not supported.");
                    previous = current;
                    continue;
                }
                var start = index;
                while (index + 1 < text.Length && "{}[],:\" \t\r\n".IndexOf(text[index + 1]) < 0) index++;
                var value = text.Substring(start, index - start + 1);
                if (value != "true" && value != "false" && value != "null" &&
                    !Regex.IsMatch(value, @"\A-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?\z"))
                    throw new InvalidDataException("Unexpected token in JSON document.");
                previous = 'v';
            }
        }
        internal static JToken Required(JObject json, string name, JTokenType type)
        {
            var value = json[name];
            if (value == null || value.Type != type) throw new InvalidDataException("Missing or invalid JSON field: " + name + ".");
            return value;
        }
        private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, int maximum, CancellationToken token)
        {
            if (response.Content == null || response.Content.Headers.ContentLength > maximum) throw new InvalidDataException("GitHub response exceeds the allowed size.");
            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                while (true)
                {
                    var read = await input.ReadAsync(buffer, 0, Math.Min(buffer.Length, maximum - (int)output.Length + 1), token).ConfigureAwait(false);
                    if (read == 0) return output.ToArray();
                    if (output.Length + read > maximum) throw new InvalidDataException("GitHub response exceeds the allowed size.");
                    output.Write(buffer, 0, read);
                }
            }
        }
        private static void EnsureSuccess(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            if ((int)response.StatusCode == 429 || response.StatusCode == HttpStatusCode.Forbidden)
            {
                var retry = response.Headers.RetryAfter;
                var timestamp = retry?.Date ?? (retry?.Delta != null ? DateTimeOffset.UtcNow + retry.Delta.Value : DateTimeOffset.UtcNow.AddHours(1));
                if (retry == null && response.Headers.TryGetValues("X-RateLimit-Reset", out var values) && long.TryParse(values.FirstOrDefault(), out var unix))
                {
                    try { timestamp = DateTimeOffset.FromUnixTimeSeconds(unix); } catch (ArgumentOutOfRangeException) { }
                }
                throw new UpdateRateLimitException(timestamp);
            }
            response.EnsureSuccessStatusCode();
        }
        public void Dispose() { client.Dispose(); }
    }
    internal sealed class UpdateRateLimitException : HttpRequestException
    {
        public DateTimeOffset RetryAfter { get; }
        public UpdateRateLimitException(DateTimeOffset retryAfter) : base("GitHub update checks are limited. Retry after " + retryAfter.ToUniversalTime().ToString("u") + ".") { RetryAfter = retryAfter; }
    }
}
