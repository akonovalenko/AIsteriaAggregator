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
        private readonly string _model;

        public string Name => "Google Gemini";
        public bool SupportsImages => true;

        public GeminiProvider(string endpointUrl, string key, string model)
        {
            _endpointUrl = endpointUrl;
            _apiKey = key;
            _model = model;
        }

        public async Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_apiKey)) return AiResponse.Fail("API key not set");

            try
            {
                var input = new List<object>();
                input.Add(new { type = "text", text = prompt });

                if (images != null)
                    foreach (var img in images)
                        input.Add(new
                        {
                            type = "image",
                            mime_type = img.Mime ?? "image/jpeg",
                            data = Convert.ToBase64String(img.Data)
                        });

                var body = new Dictionary<string, object>
                {
                    ["model"] = _model,
                    ["input"] = input.Count == 1 ? input[0] : input.ToArray(),
                    ["system_instruction"] = ProviderOptions.SystemInstruction,
                    ["store"] = false
                };

                if (ProviderOptions.MaxTokens.HasValue)
                    body["generation_config"] = new Dictionary<string, object>
                    {
                        ["max_output_tokens"] = ProviderOptions.MaxTokens.Value
                    };

                string bodyJson = JsonSerializer.Serialize(body);
                using var req = new HttpRequestMessage(HttpMethod.Post, NormalizeEndpoint(_endpointUrl))
                {
                    Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
                };
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

                using var doc = JsonDocument.Parse(json);
                string text = null;
                if (doc.RootElement.TryGetProperty("steps", out var steps))
                {
                    foreach (var step in steps.EnumerateArray())
                    {
                        if (!step.TryGetProperty("type", out var type) || type.GetString() != "model_output") continue;
                        if (!step.TryGetProperty("content", out var content)) continue;
                        foreach (var block in content.EnumerateArray())
                        {
                            if (block.TryGetProperty("type", out var blockType) && blockType.GetString() == "text" &&
                                block.TryGetProperty("text", out var value))
                            {
                                text = value.GetString();
                                if (!string.IsNullOrWhiteSpace(text)) break;
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(text)) break;
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                    return AiResponse.Fail("Error: no text output in Gemini response");

                int? promptTokens = null, completionTokens = null;
                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("total_input_tokens", out var inputTokens) && inputTokens.TryGetInt32(out var iv)) promptTokens = iv;
                    if (usage.TryGetProperty("total_output_tokens", out var outputTokens) && outputTokens.TryGetInt32(out var ov)) completionTokens = ov;
                }

                return AiResponse.Ok(text, promptTokens, completionTokens);
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

        private static string NormalizeEndpoint(string endpoint)
        {
            endpoint = (endpoint ?? string.Empty).TrimEnd('/');
            if (endpoint.EndsWith("/interactions", StringComparison.OrdinalIgnoreCase)) return endpoint;
            if (endpoint.Contains("/models/", StringComparison.OrdinalIgnoreCase))
                return "https://generativelanguage.googleapis.com/v1beta/interactions";
            return endpoint + "/interactions";
        }
    }
}
