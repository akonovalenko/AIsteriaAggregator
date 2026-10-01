namespace Aisteria.Providers
{
    public class OpenAIProvider : OpenAICompatibleProvider
    {
        public override string Name => "OpenAI";

        public OpenAIProvider(string baseUrl, string apiKey, string model, string visionModel)
            : base(baseUrl, model, apiKey, visionModel) { }
    }
}
