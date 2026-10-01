namespace Aisteria.Providers
{
    public class NvidiaProvider : OpenAICompatibleProvider
    {
        public override string Name => "NVIDIA NIM";

        public NvidiaProvider(string baseUrl, string apiKey, string model, string visionModel)
            : base(baseUrl, model, apiKey, visionModel) { }
    }
}
