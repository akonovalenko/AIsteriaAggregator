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
        protected string ApiKey { get; }
        protected string BaseUrl { get; }
        protected string Model { get; }
        protected string VisionModel { get; }

        protected virtual int? GetMaxTokens() => ProviderOptions.MaxTokens;
        protected virtual int GetMaxAttempts() => 5;
        protected virtual void ConfigureRequestBody(Dictionary<string, object> body) { }
        protected virtual string GetErrorMessage(string json, int statusCode, HttpResponseMessage response) =>
            Http.ErrorMessage(json, statusCode);

        public abstract string Name { get; }
        public bool SupportsImages => VisionModel != null;

        /// <summary>Optional user override for the text model (empty = built-in default).</summary>
        public string ModelOverride { get; set; }

        protected OpenAICompatibleProvider(string baseUrl, string model, string apiKey, string visionModel = null)
        {
            BaseUrl = baseUrl ?? string.Empty;
            Model = model ?? string.Empty;
            ApiKey = apiKey ?? string.Empty;
            VisionModel = visionModel;
        }

        public async Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(ApiKey)) return AiResponse.Fail("API key not set");

            bool   useImages = images != null && images.Count > 0 && VisionModel != null;
            string textModel = string.IsNullOrWhiteSpace(ModelOverride) ? Model : ModelOverride.Trim();
            string activeModel = useImages ? VisionModel : textModel;

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

            // Build body with optional generation settings.
            var body = new Dictionary<string, object> { ["model"] = activeModel, ["messages"] = messages };
            if (ProviderOptions.Temperature.HasValue) body["temperature"] = ProviderOptions.Temperature.Value;
            var maxTokens = GetMaxTokens();
            if (maxTokens.HasValue) body["max_tokens"] = maxTokens.Value;
            ConfigureRequestBody(body);
            string bodyJson = JsonSerializer.Serialize(body);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(100));

                HttpResponseMessage response = null;
                string json = null;
                int maxAttempts = Math.Max(1, GetMaxAttempts());

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(BaseUrl))
                    {
                        Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                    };
                    request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {ApiKey}");

                    response?.Dispose();
                    response = await Http.Client.SendAsync(request, timeoutCts.Token);
                    json = await response.Content.ReadAsStringAsync(timeoutCts.Token);

                    if ((int)response.StatusCode != 429 || attempt == maxAttempts - 1) break;

                    var delay = Http.GetRetryDelay(response) ??
                                TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt)));
                    try { Logger.Instance.LogDiagnostics(this, $"HTTP 429 from {Name}. Retry {attempt + 1}/{maxAttempts - 1} in {delay.TotalSeconds:0.#}s."); } catch { }
                    await Task.Delay(delay, timeoutCts.Token);
                }

                try
                {
                    Logger.Instance.LogRequestResponse(this, null, response, json);
                }
                catch { }

                if (!response.IsSuccessStatusCode)
                {
                    var message = GetErrorMessage(json, (int)response.StatusCode, response);
                    response.Dispose();
                    return AiResponse.Fail(message);
                }

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
                    response.Dispose();
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

        private static string BuildChatCompletionsUrl(string baseUrl)
        {
            var url = (baseUrl ?? string.Empty).TrimEnd('/');
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return url;
            return url + "/chat/completions";
        }
    }
}
