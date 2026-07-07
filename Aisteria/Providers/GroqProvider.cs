namespace Aisteria.Providers
{
    public class GroqProvider : OpenAICompatibleProvider
    {
        public override string Name => "Groq (Llama 3.3)";

        public GroqProvider(string baseUrl, string apiKey)
            : base(baseUrl, "llama-3.3-70b-versatile", apiKey, "meta-llama/llama-4-scout-17b-16e-instruct") { }
    }
}
