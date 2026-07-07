namespace Aisteria.Providers
{
    public class DeepSeekProvider : OpenAICompatibleProvider
    {
        public override string Name => "DeepSeek";

        public DeepSeekProvider(string baseUrl, string apiKey)
            : base(baseUrl, "deepseek-chat", apiKey) { }
    }
}
