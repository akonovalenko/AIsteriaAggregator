namespace Aisteria.Providers
{
    // Perplexity's API is OpenAI-compatible (POST {baseUrl}/chat/completions).
    // "sonar" models answer with live web search. Text only (no image input).
    public class PerplexityProvider : OpenAICompatibleProvider
    {
        public override string Name => "Perplexity (Sonar)";

        public PerplexityProvider(string baseUrl, string apiKey)
            : base(baseUrl, "sonar", apiKey) { }
    }
}
