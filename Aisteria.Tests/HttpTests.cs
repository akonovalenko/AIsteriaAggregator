using Aisteria.Providers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aisteria.Tests
{
    [TestClass]
    public class HttpTests
    {
        [TestMethod]
        public void ErrorMessage_ExtractsProviderErrorMessage()
        {
            var json = "{\"error\":{\"message\":\"Invalid API key\"}}";
            Assert.AreEqual("Error: Invalid API key", Http.ErrorMessage(json, 401));
        }

        [TestMethod]
        public void ErrorMessage_NonJsonBody_FallsBackToRaw()
        {
            Assert.AreEqual("Error: HTTP 502: Bad Gateway", Http.ErrorMessage("Bad Gateway", 502));
        }

        [TestMethod]
        public void ErrorMessage_EmptyBody_UsesStatusOnly()
        {
            Assert.AreEqual("Error: HTTP 500: ", Http.ErrorMessage("", 500));
        }

        [TestMethod]
        public void ErrorMessage_JsonWithoutErrorMessage_FallsBackToRaw()
        {
            Assert.AreEqual("Error: HTTP 400: {\"foo\":1}", Http.ErrorMessage("{\"foo\":1}", 400));
        }

        [TestMethod]
        public void ErrorMessage_LongBody_IsTruncated()
        {
            var body   = new string('x', 900);
            var result = Http.ErrorMessage(body, 500);

            Assert.IsTrue(result.EndsWith("…"), "long body should be truncated with an ellipsis");
            Assert.IsTrue(result.Length < 600, "truncated result should be capped near 500 chars");
        }
    }
}
