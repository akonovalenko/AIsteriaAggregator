using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Aisteria.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aisteria.Tests
{
    [TestClass]
    public class ProviderTests
    {
        private sealed class FakeProvider : OpenAICompatibleProvider
        {
            public FakeProvider(string key, string visionModel)
                : base("http://localhost", "model", key, visionModel) { }
            public override string Name => "Fake";
        }

        private sealed class TestMistralProvider : MistralProvider
        {
            public TestMistralProvider()
                : base("https://api.mistral.ai/v1", "key", "mistral-small-latest", null) { }

            public int MaxAttempts => GetMaxAttempts();
            public int? MaxTokens => GetMaxTokens();
            public void Configure(Dictionary<string, object> body) => ConfigureRequestBody(body);
            public string Error(string json, int statusCode, HttpResponseMessage response) =>
                GetErrorMessage(json, statusCode, response);
        }

        private sealed class TestOllamaCloudProvider : OllamaCloudProvider
        {
            public TestOllamaCloudProvider()
                : base("https://ollama.com/v1", "key", "gpt-oss:120b-cloud", null) { }

            public string Error(string json, int statusCode, HttpResponseMessage response) =>
                GetErrorMessage(json, statusCode, response);
        }

        [TestMethod]
        public async Task OpenAiCompatible_EmptyKey_ReturnsApiKeyNotSet()
        {
            var provider = new FakeProvider("", null);
            var resp = await provider.AskAsync("hi");

            Assert.IsTrue(resp.IsError);
            Assert.AreEqual("API key not set", resp.Text);
        }

        [TestMethod]
        public void OpenAiCompatible_SupportsImages_FollowsVisionModel()
        {
            Assert.IsTrue(new FakeProvider("k", "vision-model").SupportsImages);
            Assert.IsFalse(new FakeProvider("k", null).SupportsImages);
        }

        [TestMethod]
        public void Mistral_NameAndRateLimitPolicy_AreCurrent()
        {
            var provider = new TestMistralProvider();

            Assert.AreEqual("Mistral", provider.Name);
            Assert.AreEqual(1, provider.MaxAttempts);
        }

        [TestMethod]
        public void Mistral_MaxTokens_IsCappedAt2048()
        {
            var old = ProviderOptions.MaxTokens;
            try
            {
                ProviderOptions.MaxTokens = 8192;
                Assert.AreEqual(2048, new TestMistralProvider().MaxTokens);

                ProviderOptions.MaxTokens = 1024;
                Assert.AreEqual(1024, new TestMistralProvider().MaxTokens);
            }
            finally
            {
                ProviderOptions.MaxTokens = old;
            }
        }

        [TestMethod]
        public void Mistral_Small_DisablesReasoning()
        {
            var body = new Dictionary<string, object> { ["model"] = "mistral-small-latest" };
            new TestMistralProvider().Configure(body);

            Assert.AreEqual("none", body["reasoning_effort"]);
        }

        [TestMethod]
        public void Mistral_429_AddsRateLimitContext()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("x-ratelimit-limit-req-minute", "10");
            response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-req-minute", "0");

            var result = new TestMistralProvider().Error(
                "{\"error\":{\"message\":\"Rate limit exceeded\"}}",
                429,
                response);

            StringAssert.Contains(result, "Rate limit exceeded");
            StringAssert.Contains(result, "10/min");
            StringAssert.Contains(result, "remaining: 0");
            StringAssert.Contains(result, "Mistral-side rate limit");
        }

        [TestMethod]
        public void OllamaCloud_405_ExplainsCorrectEndpoint()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
            var result = new TestOllamaCloudProvider().Error("Method Not Allowed", 405, response);

            StringAssert.Contains(result, "HTTP 405");
            StringAssert.Contains(result, "https://ollama.com/v1");
        }

        [TestMethod]
        public async Task Gemini_EmptyKey_ReturnsApiKeyNotSet()
        {
            var provider = new GeminiProvider(
                "https://generativelanguage.googleapis.com/v1beta/interactions",
                "",
                "gemini-3.8-flash");

            var resp = await provider.AskAsync("hi");

            Assert.IsTrue(resp.IsError);
            Assert.AreEqual("API key not set", resp.Text);
        }

        [TestMethod]
        public void Gemini_HasCurrentModelAndImageSupport()
        {
            var provider = new GeminiProvider(
                "https://generativelanguage.googleapis.com/v1beta/interactions",
                "key",
                "gemini-3.8-flash");

            Assert.AreEqual("Google Gemini", provider.Name);
            Assert.IsTrue(provider.SupportsImages);
        }
    }
}
