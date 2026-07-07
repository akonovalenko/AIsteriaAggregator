namespace Aisteria.Providers
{
    public class OllamaCloudProvider : OpenAICompatibleProvider
    {
        public override string Name => "Ollama Cloud";
        public OllamaCloudProvider(string baseUrl, string apiKey)
            : base(baseUrl, "llama3.2", apiKey, visionModel: "llama3.2-vision") { }
    }
}
