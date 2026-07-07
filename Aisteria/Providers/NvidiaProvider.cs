namespace Aisteria.Providers
{
    public class NvidiaProvider : OpenAICompatibleProvider
    {
        public override string Name => "NVIDIA NIM";

        public NvidiaProvider(string baseUrl, string apiKey)
            : base(baseUrl, "meta/llama-3.3-70b-instruct", apiKey, visionModel: "nvidia/llama-3.2-90b-vision-instruct") { }
    }
}
