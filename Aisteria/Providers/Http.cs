using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;

namespace Aisteria.Providers
{
    /// <summary>
    /// Single shared HttpClient for the whole application. Creating a new HttpClient
    /// per request exhausts sockets (each disposed client leaves a socket in TIME_WAIT),
    /// which under repeated use causes intermittent "connection failed" errors.
    ///
    /// The client-level timeout is disabled; callers pass a linked CancellationToken
    /// with CancelAfter(...) so each endpoint can use its own timeout.
    /// </summary>
    internal static class Http
    {
        /// <summary>System instruction sent with every request so models reply in the user's language
        /// (some models — e.g. DeepSeek R1 — otherwise default to Chinese).</summary>
        public const string LanguageInstruction = "Always respond in the same language as the user's message. If the language is unclear, respond in English. Never answer in any other language.";

        public static readonly HttpClient Client = Create();

        private static HttpClient Create()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5) // periodically re-resolve DNS
            };
            return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        }

        /// <summary>Extracts an "error.message" from an OpenAI/Gemini-style error body, or falls back to the raw (truncated) body.</summary>
        public static string ErrorMessage(string json, int statusCode)
        {
            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("error", out var err) &&
                        err.TryGetProperty("message", out var msg))
                        return $"Error: {msg.GetString()}";
                }
                catch (JsonException) { /* not JSON — fall through to raw body */ }
            }

            var body = json ?? string.Empty;
            if (body.Length > Aisteria.Constants.UiTruncateLength) body = body.Substring(0, Aisteria.Constants.UiTruncateLength) + "…";
            return $"Error: HTTP {statusCode}: {body}";
        }

        /// <summary>Reads OpenAI-style token usage (usage.prompt_tokens / usage.completion_tokens) from a parsed root element.</summary>
        public static (int? prompt, int? completion) ReadUsage(JsonElement root)
        {
            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                int? p = usage.TryGetProperty("prompt_tokens", out var pt) && pt.TryGetInt32(out var pv) ? pv : (int?)null;
                int? c = usage.TryGetProperty("completion_tokens", out var cot) && cot.TryGetInt32(out var cv) ? cv : (int?)null;
                return (p, c);
            }
            return (null, null);
        }
    }
}
