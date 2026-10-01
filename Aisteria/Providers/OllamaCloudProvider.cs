using System.Net;
using System.Net.Http;

namespace Aisteria.Providers
{
    public class OllamaCloudProvider : OpenAICompatibleProvider
    {
        public override string Name => "Ollama Cloud";

        public OllamaCloudProvider(string baseUrl, string apiKey, string model, string visionModel)
            : base(baseUrl, model, apiKey, visionModel) { }

        protected override string GetErrorMessage(string json, int statusCode, HttpResponseMessage response)
        {
            if (statusCode == (int)HttpStatusCode.MethodNotAllowed)
                return "Error: Ollama Cloud rejected the request (HTTP 405). Use https://ollama.com/v1 as the OpenAI-compatible endpoint.";

            return base.GetErrorMessage(json, statusCode, response);
        }
    }
}
