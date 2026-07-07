namespace Aisteria.Providers
{
    public class GitHubModelsProvider : OpenAICompatibleProvider
    {
        public override string Name => "GitHub Models";

        // Key is a GitHub Personal Access Token (PAT) with Models read permission.
        public GitHubModelsProvider(string baseUrl, string pat)
            : base(baseUrl, "gpt-4o-mini", pat, visionModel: "gpt-4o") { }
    }
}
