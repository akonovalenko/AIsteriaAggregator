using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aisteria.Providers
{
    public class GeminiProvider : IAIProvider
    {
        private readonly string _apiKey;
        private readonly string _endpointUrl;

        public string Name           => "Google Gemini";
        public bool   SupportsImages => true;

        public GeminiProvider(string endpointUrl, string key)
        {
            _endpointUrl = endpointUrl;
            _apiKey      = key;
        }

        public async Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey)) return AiResponse.Fail("API key not set");

            try
            {
                // Gemini has no separate system role here → fold the instruction into the prompt.
                string effectivePrompt = ProviderOptions.SystemInstruction + "\n\n" + prompt;
                var parts = new List<object> { new { text = effectivePrompt } };
                if (images != null)
                    foreach (var img in images)
                        parts.Add(new { inline_data = new { mime_type = img.Mime ?? "image/jpeg", data = Convert.ToBase64String(img.Data) } });

                var body = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["contents"] = new[] { new { parts = parts.ToArray() } }
                };
                if (ProviderOptions.Temperature.HasValue || ProviderOptions.MaxTokens.HasValue)
                {
                    var cfg = new System.Collections.Generic.Dictionary<string, object>();
                    if (ProviderOptions.Temperature.HasValue) cfg["temperature"]     = ProviderOptions.Temperature.Value;
                    if (ProviderOptions.MaxTokens.HasValue)   cfg["maxOutputTokens"] = ProviderOptions.MaxTokens.Value;
                    body["generationConfig"] = cfg;
                }
                string bodyJson = JsonSerializer.Serialize(body);

                using var req = new HttpRequestMessage(HttpMethod.Post, _endpointUrl)
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                // Key in a header, not the query string (query strings get logged by proxies/servers)
                req.Headers.TryAddWithoutValidation("x-goog-api-key", _apiKey);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(100));

                var response = await Http.Client.SendAsync(req, timeoutCts.Token);
                var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Instance.LogRequestResponse(this, req, response, json);
                    return AiResponse.Fail(Http.ErrorMessage(json, (int)response.StatusCode));
                }

                JsonDocument doc;
                try { doc = JsonDocument.Parse(json); }
                catch (JsonException) { return AiResponse.Fail("Error: invalid JSON in response"); }

                using (doc)
                {
                    if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                        return AiResponse.Fail("Error: no candidates in response (content may have been filtered)");

                    var partsEl = candidates[0].GetProperty("content").GetProperty("parts");
                    if (partsEl.GetArrayLength() == 0)
                        return AiResponse.Fail("Error: no parts in response");

                    var text = partsEl[0].GetProperty("text").GetString();
                    if (string.IsNullOrWhiteSpace(text)) return AiResponse.Fail("Error: empty text in response");

                    int? pt = null, cot = null;
                    if (doc.RootElement.TryGetProperty("usageMetadata", out var um))
                    {
                        if (um.TryGetProperty("promptTokenCount", out var p) && p.TryGetInt32(out var pv)) pt = pv;
                        if (um.TryGetProperty("candidatesTokenCount", out var c) && c.TryGetInt32(out var cv)) cot = cv;
                    }
                    return AiResponse.Ok(text, pt, cot);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return AiResponse.Fail("Error: request timed out");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                return AiResponse.Fail($"Error: connection failed ({ex.InnerException?.Message ?? ex.Message})");
            }
            catch (Exception ex)
            {
                return AiResponse.Fail($"Error: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
