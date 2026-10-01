using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Aisteria.Providers
{
    public class MistralProvider : OpenAICompatibleProvider
    {
        public override string Name => "Mistral";

        // Mistral rate limits are server-side. Do not hammer a rate-limited key
        // with the generic provider retry loop; return the actual 429 immediately.
        protected override int GetMaxAttempts() => 1;

        protected override int? GetMaxTokens() =>
            ProviderOptions.MaxTokens.HasValue
                ? Math.Min(ProviderOptions.MaxTokens.Value, 2048)
                : 2048;

        // Mistral Small supports adjustable reasoning; disabling it avoids spending
        // the token budget on hidden reasoning for a normal chat aggregator.
        protected override void ConfigureRequestBody(Dictionary<string, object> body)
        {
            if (body.TryGetValue("model", out var model) &&
                model is string name && name.StartsWith("mistral-small", StringComparison.OrdinalIgnoreCase))
                body["reasoning_effort"] = "none";
        }

        protected override string GetErrorMessage(string json, int statusCode, HttpResponseMessage response)
        {
            var message = Http.ErrorMessage(json, statusCode);
            if (statusCode != 429) return message;

            var limit = GetHeader(response, "x-ratelimit-limit-req-minute");
            var remaining = GetHeader(response, "x-ratelimit-remaining-req-minute");
            var suffix = string.IsNullOrWhiteSpace(limit) && string.IsNullOrWhiteSpace(remaining)
                ? ""
                : $" Mistral request limit: {limit ?? "?"}/min, remaining: {remaining ?? "?"}.";

            return message + suffix + " This is a Mistral-side rate limit; wait for the limit to reset or check the API limits/billing for this key.";
        }

        private static string GetHeader(HttpResponseMessage response, string name) =>
            response != null && response.Headers.TryGetValues(name, out var values)
                ? string.Join(",", values)
                : null;

        public MistralProvider(string baseUrl, string apiKey, string model, string visionModel)
            : base(baseUrl, model, apiKey, visionModel) { }
    }
}
