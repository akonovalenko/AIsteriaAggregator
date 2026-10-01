namespace Aisteria.Providers
{
    public class OpenRouterProvider : OpenAICompatibleProvider
    {
        private const int SafeMaxTokens = 8192;

        public override string Name => "OpenRouter";

        public OpenRouterProvider(string baseUrl, string apiKey, string model, string visionModel)
            : base(baseUrl, model, apiKey, visionModel) { }

        protected override int? GetMaxTokens()
        {
            var value = base.GetMaxTokens();
            return value.HasValue ? System.Math.Min(value.Value, SafeMaxTokens) : null;
        }
    }
}
