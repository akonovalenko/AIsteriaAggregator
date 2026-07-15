using System;
using System.IO;
using System.Text.RegularExpressions;
using Aisteria.Providers;
using Aisteria.Models;

namespace Aisteria
{
    internal class Logger : ILogger
    {
        /// <summary>
        /// Public injectable logger instance. By default this references a concrete <see cref="Logger"/> instance
        /// but callers may replace <c>Logger.Instance</c> with a custom <see cref="ILogger"/> (for DI or testing).
        /// </summary>
        public static ILogger Instance { get; set; } = new Logger();

        private readonly object _lock = new object();
        private const long MaxBytes = 1_000_000; // rotate when > ~1MB
        private const int MaxFiles = 10;

        private string LogDirectory
        {
            get
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aisteria", "logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private string CurrentLogPath => Path.Combine(LogDirectory, "diagnostics.log");

         /// <summary>
        /// Appends a timestamped log entry to the current log file. Ensures log rotation is performed
        /// by calling <c>RotateIfNeeded</c> before writing.
        /// </summary>
        /// <param name="text">The message to append to the log. Nulls and I/O errors are handled internally; callers will not receive exceptions from this method.</param>
        /// <returns>
        /// The path to the log file that was (or would have been) written to — the value of <c>CurrentLogPath</c>.
        /// On failure this method returns the last known <c>CurrentLogPath</c> as a best-effort result.
        /// </returns>
        /// <remarks>
        /// - Thread-safe: access is synchronized using the internal <c>_lock</c> object.
        /// - Timestamps are written in UTC using the format "yyyy-MM-dd HH:mm:ss".
        /// - Each entry is followed by a blank line.
        /// - Exceptions from file I/O are swallowed to provide a best-effort logging behavior.
        /// </remarks>
        public string Log(string text)
        {
            try
            {
                lock (_lock)
                {
                    RotateIfNeeded();
                    var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {text}\n\n";
                    File.AppendAllText(CurrentLogPath, entry);
                    return CurrentLogPath;
                }
            }
            catch
            {
                return CurrentLogPath; // best-effort
            }
        }

        /// <summary>
        /// Rotate the current diagnostics log file when it exceeds the configured maximum size.
        /// This moves the existing diagnostics.log to a timestamped file and prunes older rotated files.
        /// </summary>
        private void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(CurrentLogPath)) return;
                var fi = new FileInfo(CurrentLogPath);
                if (fi.Length <= MaxBytes) return;
                var dest = Path.Combine(LogDirectory, $"diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
                File.Move(CurrentLogPath, dest);

                var files = new DirectoryInfo(LogDirectory).GetFiles("diagnostics-*.log");
                Array.Sort(files, (a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
                for (int i = MaxFiles; i < files.Length; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        /// <summary>
        /// Returns the path to the latest diagnostics log file. If the active diagnostics.log exists this
        /// path is returned; otherwise the most recent rotated diagnostics-*.log file is returned. Returns
        /// an empty string if no log file is available.
        /// </summary>
        public string GetLatestLog()
        {
            try
            {
                if (File.Exists(CurrentLogPath)) return CurrentLogPath;
                var files = new DirectoryInfo(LogDirectory).GetFiles("diagnostics-*.log");
                if (files.Length == 0) return string.Empty;
                Array.Sort(files, (a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
                return files[0].FullName;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Determines whether verbose logging is enabled globally or for the given provider name.
        /// Checks a global flag and a set of provider-specific keys derived from the provider name.
        /// </summary>
        public bool IsVerboseEnabled(string providerName)
        {
            // global flag
            if (SettingsManager.LoadFlag("VerboseLogging", false)) return true;

            if (string.IsNullOrWhiteSpace(providerName)) return false;
            string n = providerName;
            string alpha = Regex.Replace(n, "[^A-Za-z0-9]", "");
            var candidates = new[] { n, n.Replace(" ", ""), n.Split(' ')[0], alpha };
            foreach (var c in candidates)
            {
                if (string.IsNullOrWhiteSpace(c)) continue;
                if (SettingsManager.LoadFlag(c + "Verbose", false)) return true;
            }
            return false;
        }

        /// <summary>
        /// Writes provider-scoped diagnostics to the rotating log if verbose logging is enabled for the provider.
        /// The implementation masks common secret patterns before persisting the text.
        /// </summary>
        public void LogDiagnostics(IAIProvider provider, string text)
        {
            try
            {
                if (provider == null) return;
                if (!IsVerboseEnabled(provider.Name)) return;
                var header = $"Provider: {provider.Name} ({provider.GetType().FullName})\n";
                var masked = MaskSensitive(text ?? string.Empty);
                Log(header + masked);
            }
            catch { /* best-effort */ }
        }

        /// <summary>
        /// Central helper to log a single HTTP request/response interaction for a provider. Headers and
        /// bodies are masked or truncated as appropriate. This method is a no-op unless verbose logging is
        /// enabled for the provider.
        /// </summary>
        /// <param name="provider">The provider that initiated the request.</param>
        /// <param name="request">The HTTP request message (may be null).</param>
        /// <param name="response">The HTTP response message.</param>
        /// <param name="responseBody">The response body as a string.</param>
        public void LogRequestResponse(IAIProvider provider, System.Net.Http.HttpRequestMessage request, System.Net.Http.HttpResponseMessage response, string responseBody)
        {
            try
            {
                if (provider == null) return;
                if (!IsVerboseEnabled(provider.Name)) return;

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Request: {request.Method} {request.RequestUri}");
                sb.AppendLine("Request headers:");
                foreach (var h in request.Headers)
                {
                    var vals = string.Join(",", h.Value);
                    sb.AppendLine($"  {h.Key}: {(IsSensitiveHeader(h.Key) ? "<masked>" : vals)}");
                }
                if (request.Content != null)
                {
                    foreach (var h in request.Content.Headers)
                    {
                        var vals = string.Join(",", h.Value);
                        sb.AppendLine($"  {h.Key}: {(IsSensitiveHeader(h.Key) ? "<masked>" : vals)}");
                    }
                    try
                    {
                        var rc = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        if (!string.IsNullOrWhiteSpace(rc)) sb.AppendLine("Request body:\n" + Truncate(rc, 4000));
                    }
                    catch { }
                }

                sb.AppendLine($"Response: {(int)response.StatusCode} {response.ReasonPhrase}");
                sb.AppendLine("Response headers:");
                foreach (var h in response.Headers) sb.AppendLine($"  {h.Key}: {string.Join(",", h.Value)}");
                foreach (var h in response.Content.Headers) sb.AppendLine($"  {h.Key}: {string.Join(",", h.Value)}");
                sb.AppendLine("Response body:\n" + Truncate(responseBody ?? string.Empty, 4000));

                LogDiagnostics(provider, sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// Returns true when a header name looks sensitive (authorization, api keys, tokens, etc.) and
        /// its value should not be logged in clear text.
        /// </summary>
        /// <param name="headerName">the header name</param>
        private static bool IsSensitiveHeader(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName)) return false;
            var hn = headerName.Trim().ToLowerInvariant();
            return hn == "authorization" || hn == "x-api-key" || hn.Contains("key") || hn.Contains("token");
        }

        /// <summary>
        /// Truncates the supplied string to the specified maximum length and appends an ellipsis ('…') if truncation occurred.
        /// </summary>
        /// <param name="s">The input string to truncate. If <c>null</c>, an empty string is returned.</param>
        /// <param name="max">The maximum number of characters to keep from the start of <paramref name="s"/> before appending an ellipsis. Must be &gt;= 0.</param>
        /// <returns>
        /// The original string if its length is less than or equal to <paramref name="max"/>; otherwise a string containing the first <paramref name="max"/> characters of <paramref name="s"/> followed by '…'.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">May be thrown by <see cref="string.Substring(int,int)"/> when <paramref name="max"/> is negative.</exception>
        private static string Truncate(string s, int max)
        {
            if (s == null) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        /// <summary>
        /// Masks sensitive data patterns in an arbitrary text blob. This will attempt to hide Authorization
        /// headers, x-api-key values and common JSON fields such as apiKey/key/token before the text is written to disk.
        /// </summary>
        private string MaskSensitive(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            try
            {
                // Mask Authorization: Bearer <token> and x-api-key: <key>
                input = Regex.Replace(input, @"(?i)(Authorization\s*:\s*Bearer)\s+([A-Za-z0-9\-\._~+/=]+)", "$1 <masked>");
                input = Regex.Replace(input, @"(?i)(x-api-key\s*:\s*)([^\r\n]+)", "$1 <masked>");

                // Mask JSON fields like "apiKey": "..." or "key": "..."
                var jsonPattern = "\\\"(apiKey|key|apikey|token)\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"";
                input = Regex.Replace(input, jsonPattern, "\"$1\":\"<masked>\"", RegexOptions.IgnoreCase);

                return input;
            }
            catch { return input; }
        }
    }
}
