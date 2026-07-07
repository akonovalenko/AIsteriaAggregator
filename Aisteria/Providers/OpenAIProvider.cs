namespace Aisteria.Providers
{
    public class OpenAIProvider : OpenAICompatibleProvider
    {
        public override string Name => "OpenAI";

        public OpenAIProvider(string baseUrl, string apiKey)
            : base(baseUrl, "gpt-4o-mini", apiKey, visionModel: "gpt-4o") { }
    }
}
