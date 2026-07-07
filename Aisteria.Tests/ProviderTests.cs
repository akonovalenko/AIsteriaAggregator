using System.Threading.Tasks;
using Aisteria.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aisteria.Tests
{
    [TestClass]
    public class ProviderTests
    {
        // Minimal concrete subclass to exercise the shared OpenAI-compatible base.
        private sealed class FakeProvider : OpenAICompatibleProvider
        {
            public FakeProvider(string key, string visionModel)
                : base("http://localhost", "model", key, visionModel) { }
            public override string Name => "Fake";
        }

        [TestMethod]
        public async Task OpenAiCompatible_EmptyKey_ReturnsApiKeyNotSet()
        {
            // Empty key short-circuits before any HTTP call, so this needs no network.
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
        public async Task Gemini_EmptyKey_ReturnsApiKeyNotSet()
        {
            var provider = new GeminiProvider("http://localhost", "");
            var resp = await provider.AskAsync("hi");
            Assert.IsTrue(resp.IsError);
            Assert.AreEqual("API key not set", resp.Text);
        }
    }
}
