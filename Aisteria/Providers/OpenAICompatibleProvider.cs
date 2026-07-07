using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aisteria.Providers
{
    public abstract class OpenAICompatibleProvider : IAIProvider
    {
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;
        private readonly string _visionModel;

        public abstract string Name { get; }
        public bool SupportsImages => _visionModel != null;

        /// <summary>Optional user override for the text model (empty = built-in default).</summary>
        public string ModelOverride { get; set; }

        protected OpenAICompatibleProvider(string baseUrl, string model, string apiKey, string visionModel = null)
        {
            _baseUrl     = baseUrl;
            _model       = model;
            _apiKey      = apiKey;
            _visionModel = visionModel;
        }

        public async Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey)) return AiResponse.Fail("API key not set");

            bool   useImages = images != null && images.Count > 0 && _visionModel != null;
            string textModel = string.IsNullOrWhiteSpace(ModelOverride) ? _model : ModelOverride.Trim();
            string activeModel = useImages ? _visionModel : textModel;

            object[] messages;
            if (useImages)
            {
                var content = new List<object> { new { type = "text", text = prompt } };
                foreach (var img in images)
                {
                    string dataUrl = $"data:{img.Mime ?? "image/jpeg"};base64,{Convert.ToBase64String(img.Data)}";
                    content.Add(new { type = "image_url", image_url = new { url = dataUrl } });
                }
                messages = new object[]
                {
                    new { role = "system", content = ProviderOptions.SystemInstruction },
                    new { role = "user",   content = content.ToArray() }
                };
            }
            else
            {
                messages = new object[]
                {
                    new { role = "system", content = ProviderOptions.SystemInstruction },
                    new { role = "user",   content = prompt }
                };
            }

            // Build body with optional temperature / max_tokens.
            var body = new Dictionary<string, object> { ["model"] = activeModel, ["messages"] = messages };
            if (ProviderOptions.Temperature.HasValue) body["temperature"] = ProviderOptions.Temperature.Value;
            if (ProviderOptions.MaxTokens.HasValue)   body["max_tokens"]  = ProviderOptions.MaxTokens.Value;
            string bodyJson = JsonSerializer.Serialize(body);

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _baseUrl.TrimEnd('/') + "/chat/completions")
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
                // Per-request header (shared client → do not use DefaultRequestHeaders)
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_apiKey}");

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
                    if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                        return AiResponse.Fail("Error: empty choices array in response");

                    var text = choices[0].GetProperty("message").GetProperty("content").GetString();
                    if (string.IsNullOrWhiteSpace(text)) return AiResponse.Fail("Error: empty content in response");

                    var (pt, cot) = Http.ReadUsage(doc.RootElement);
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
