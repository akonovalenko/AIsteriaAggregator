namespace Aisteria.Providers
{
    public class MistralProvider : OpenAICompatibleProvider
    {
        public override string Name => "Mistral";

        public MistralProvider(string baseUrl, string apiKey)
            : base(baseUrl, "mistral-small-latest", apiKey, "pixtral-12b-2409") { }
    }
}
