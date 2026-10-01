namespace Aisteria.Providers
{
    public class DeepSeekProvider : OpenAICompatibleProvider
    {
        public override string Name => "DeepSeek";

        public DeepSeekProvider(string baseUrl, string apiKey, string model)
            : base(baseUrl, model, apiKey) { }
    }
}
