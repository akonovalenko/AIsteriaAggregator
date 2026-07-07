namespace Aisteria.Models
{
    public class Settings
    {
        public string OpenAIKey     { get; set; }
        public string GeminiKey     { get; set; }
        public string OllamaUrl     { get; set; }
        public string OllamaModel   { get; set; }
        public string GroqKey       { get; set; }
        public string DeepSeekKey   { get; set; }
        public string MistralKey    { get; set; }
        public string OpenRouterKey { get; set; }
        public string GitHubKey       { get; set; }
        public string NvidiaKey       { get; set; }
        public string OllamaCloudKey  { get; set; }
        public string ClaudeKey       { get; set; }
        public string PerplexityKey   { get; set; }

        public bool OpenAIEnabled     { get; set; } = true;
        public bool GeminiEnabled     { get; set; } = true;
        public bool OllamaEnabled     { get; set; } = true;
        public bool GroqEnabled       { get; set; } = true;
        public bool DeepSeekEnabled   { get; set; } = true;
        public bool MistralEnabled    { get; set; } = true;
        public bool OpenRouterEnabled { get; set; } = true;
        public bool GitHubEnabled       { get; set; } = true;
        public bool NvidiaEnabled       { get; set; } = true;
        public bool OllamaCloudEnabled  { get; set; } = true;
        public bool ClaudeEnabled       { get; set; } = true;
        public bool PerplexityEnabled   { get; set; } = true;
    }
}
