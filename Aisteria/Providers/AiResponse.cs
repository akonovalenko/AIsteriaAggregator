namespace Aisteria.Providers
{
    /// <summary>Result of a single provider call: text plus optional token usage.</summary>
    public sealed class AiResponse
    {
        public string Text             { get; set; }
        public bool   IsError          { get; set; }
        public int?   PromptTokens     { get; set; }
        public int?   CompletionTokens { get; set; }

        public int? TotalTokens =>
            (PromptTokens == null && CompletionTokens == null)
                ? (int?)null
                : (PromptTokens ?? 0) + (CompletionTokens ?? 0);

        public static AiResponse Fail(string message) =>
            new AiResponse { Text = message, IsError = true };

        public static AiResponse Ok(string text, int? promptTokens = null, int? completionTokens = null) =>
            new AiResponse { Text = text, IsError = false, PromptTokens = promptTokens, CompletionTokens = completionTokens };
    }
}
