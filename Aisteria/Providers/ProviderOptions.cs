namespace Aisteria.Providers
{
    /// <summary>
    /// Global generation options applied to every provider request. Set from Settings
    /// (in MainWindow) before/while providers are (re)created. Null = use provider default.
    /// </summary>
    public static class ProviderOptions
    {
        public static double? Temperature { get; set; }
        public static int?    MaxTokens   { get; set; }
        public static string  ExtraSystem { get; set; }

        /// <summary>Language rule + the user's optional extra system prompt.</summary>
        public static string SystemInstruction =>
            string.IsNullOrWhiteSpace(ExtraSystem)
                ? Http.LanguageInstruction
                : Http.LanguageInstruction + "\n\n" + ExtraSystem.Trim();
    }
}
