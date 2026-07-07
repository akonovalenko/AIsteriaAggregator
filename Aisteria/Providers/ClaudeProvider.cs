using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aisteria.Providers
{
    // Anthropic Claude via the Messages API. Not OpenAI-compatible: it uses the
    // x-api-key + anthropic-version headers and a {content: [text|image]} message shape.
    public class ClaudeProvider : IAIProvider
    {
        private const string Model     = "claude-haiku-4-5-20251001";
        private const int    MaxTokens = 1024;

        private readonly string _apiKey;
        private readonly string _baseUrl;

        public string Name           => "Claude (Haiku 4.5)";
        public bool   SupportsImages => true;

        public ClaudeProvider(string baseUrl, string apiKey)
        {
            _baseUrl = baseUrl;
            _apiKey  = apiKey;
        }

        public async Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey)) return AiResponse.Fail("API key not set");

            object content;
            if (images != null && images.Count > 0)
            {
                var blocks = new List<object> { new { type = "text", text = prompt } };
                foreach (var img in images)
                    blocks.Add(new
                    {
                        type   = "image",
                        source = new { type = "base64", media_type = img.Mime ?? "image/jpeg", data = Convert.ToBase64String(img.Data) }
                    });
                content = blocks.ToArray();
            }
            else
            {
                content = prompt; // Anthropic accepts a plain string for text-only messages
            }

            var body = new System.Collections.Generic.Dictionary<string, object>
            {
                ["model"]      = Model,
                ["max_tokens"] = ProviderOptions.MaxTokens ?? MaxTokens,
                ["system"]     = ProviderOptions.SystemInstruction,
                ["messages"]   = new[] { new { role = "user", content } }
            };
            if (ProviderOptions.Temperature.HasValue) body["temperature"] = ProviderOptions.Temperature.Value;
            string bodyJson = JsonSerializer.Serialize(body);

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl.TrimEnd('/') + "/v1/messages")
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
                req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(100));

                var response = await Http.Client.SendAsync(req, timeoutCts.Token);
                var json = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                    return AiResponse.Fail(Http.ErrorMessage(json, (int)response.StatusCode));

                JsonDocument doc;
                try { doc = JsonDocument.Parse(json); }
                catch (JsonException) { return AiResponse.Fail("Error: invalid JSON in response"); }

                using (doc)
                {
                    if (!doc.RootElement.TryGetProperty("content", out var blocks) || blocks.GetArrayLength() == 0)
                        return AiResponse.Fail("Error: empty content in response");

                    int? pt = null, cot = null;
                    if (doc.RootElement.TryGetProperty("usage", out var usage))
                    {
                        if (usage.TryGetProperty("input_tokens", out var i) && i.TryGetInt32(out var iv)) pt = iv;
                        if (usage.TryGetProperty("output_tokens", out var o) && o.TryGetInt32(out var ov)) cot = ov;
                    }

                    foreach (var block in blocks.EnumerateArray())
                    {
                        if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                            block.TryGetProperty("text", out var txt))
                        {
                            var s = txt.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) return AiResponse.Ok(s, pt, cot);
                        }
                    }
                    return AiResponse.Fail("Error: no text block in response");
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
