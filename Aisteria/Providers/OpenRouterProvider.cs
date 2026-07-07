namespace Aisteria.Providers
{
    public class OpenRouterProvider : OpenAICompatibleProvider
    {
        public override string Name => "OpenRouter (DeepSeek R1)";

        public OpenRouterProvider(string baseUrl, string apiKey)
            // Vision uses OpenRouter's free-model router, which auto-selects an available
            // free model that supports image input — robust against free-slug churn.
            : base(baseUrl, "deepseek/deepseek-r1", apiKey, "openrouter/free") { }
    }
}
