using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aisteria.Providers
{
    public class OllamaProvider : IAIProvider
    {
        private readonly string _baseUrl;
        private readonly string _preferredModel;

        public string Name           => "Ollama Local Model";
        public bool   SupportsImages => true;

        public OllamaProvider(string ollamaUrl, string preferredModel = null)
        {
            _baseUrl        = ollamaUrl.TrimEnd('/');
            _preferredModel = preferredModel;
        }

        public async Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_baseUrl)) return AiResponse.Fail("Ollama URL not set");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            try
            {
                var model = await ResolveModelAsync(client, ct);
                if (model == null) return AiResponse.Fail("Error: no models installed in Ollama");

                string bodyJson;
                object opts = null;
                if (ProviderOptions.Temperature.HasValue || ProviderOptions.MaxTokens.HasValue)
                {
                    var o = new System.Collections.Generic.Dictionary<string, object>();
                    if (ProviderOptions.Temperature.HasValue) o["temperature"] = ProviderOptions.Temperature.Value;
                    if (ProviderOptions.MaxTokens.HasValue)   o["num_predict"] = ProviderOptions.MaxTokens.Value;
                    opts = o;
                }

                var body = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["model"]  = model,
                    ["prompt"] = prompt,
                    ["system"] = ProviderOptions.SystemInstruction,
                    ["stream"] = false
                };
                if (images != null && images.Count > 0)
                    body["images"] = images.Select(i => Convert.ToBase64String(i.Data)).ToArray();
                if (opts != null) body["options"] = opts;
                bodyJson = JsonSerializer.Serialize(body);

                var response = await client.PostAsync(
                    _baseUrl + "/api/generate",
                    new StringContent(bodyJson, Encoding.UTF8, "application/json"),
                    ct
                );
                var json = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    try
                    {
                        Logger.Instance.LogRequestResponse(this, null, response, json);
                    }
                    catch { }
                    return AiResponse.Fail($"Error {(int)response.StatusCode}: {json}");
                }

                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("response", out var result))
                    return AiResponse.Fail($"Error: unexpected response format: {json}");

                var text = result.GetString();
                if (string.IsNullOrWhiteSpace(text)) return AiResponse.Fail("Error: empty response from Ollama");

                int? pt = null, cot = null;
                if (doc.RootElement.TryGetProperty("prompt_eval_count", out var p) && p.TryGetInt32(out var pv)) pt = pv;
                if (doc.RootElement.TryGetProperty("eval_count", out var c) && c.TryGetInt32(out var cv)) cot = cv;
                return AiResponse.Ok(text, pt, cot);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return AiResponse.Fail("Error: request timed out");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (HttpRequestException ex) { return AiResponse.Fail($"Error: Ollama unavailable ({ex.InnerException?.Message ?? ex.Message})"); }
            catch (Exception ex)            { return AiResponse.Fail($"Error: {ex.GetType().Name}: {ex.Message}"); }
        }

        private async Task<string> ResolveModelAsync(HttpClient client, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(_preferredModel))
                return _preferredModel;

            var response = await client.GetAsync(_baseUrl + "/api/tags", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var models = doc.RootElement.GetProperty("models");
            if (models.GetArrayLength() == 0) return null;
            return models[0].GetProperty("name").GetString();
        }
    }
}
