namespace Aisteria.Providers
{
    public class GroqProvider : OpenAICompatibleProvider
    {
        public override string Name => "Groq";

        public GroqProvider(string baseUrl, string apiKey, string model, string visionModel)
            : base(baseUrl, model, apiKey, visionModel) { }
    }
}
