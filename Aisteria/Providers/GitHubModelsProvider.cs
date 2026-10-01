namespace Aisteria.Providers
{
    public class GitHubModelsProvider : OpenAICompatibleProvider
    {
        public override string Name => "GitHub Models";

        // Key is a GitHub Personal Access Token (PAT) with Models read permission.
        public GitHubModelsProvider(string baseUrl, string pat, string model, string visionModel)
            : base(baseUrl, model, pat, visionModel) { }
    }
}
