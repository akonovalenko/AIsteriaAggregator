namespace Aisteria.Providers
{
    public class NvidiaProvider : OpenAICompatibleProvider
    {
        public override string Name => "NVIDIA NIM";

        public NvidiaProvider(string baseUrl, string apiKey)
            : base(baseUrl, "meta/llama-3.3-70b-instruct", apiKey, visionModel: "nvidia/llama-3.2-90b-vision-instruct") { }
        // NvidiaProvider uses the OpenAICompatibleProvider HTTP paths. Per-request diagnostics are
        // handled via the centralized Logger.LogRequestResponse from call sites (same approach as
        // OllamaProvider). This class does not persist diagnostics itself.
    }
}
