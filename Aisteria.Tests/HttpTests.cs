using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
            var body = new string('x', 900);
            var result = Http.ErrorMessage(body, 500);

            Assert.IsTrue(result.EndsWith("…"));
            Assert.IsTrue(result.Length < 600);
        }

        [TestMethod]
        public void GetRetryDelay_UsesRetryAfterDelta()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

            Assert.AreEqual(TimeSpan.FromSeconds(7), Http.GetRetryDelay(response));
        }

        [TestMethod]
        public void GetRetryDelay_ClampsLargeDelayTo30Seconds()
        {
            using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));

            Assert.AreEqual(TimeSpan.FromSeconds(30), Http.GetRetryDelay(response));
        }

        [TestMethod]
        public void GetRetryDelay_NullResponse_ReturnsNull()
        {
            Assert.IsNull(Http.GetRetryDelay(null));
        }

        [TestMethod]
        public void ReadUsage_ReadsOpenAiTokenFields()
        {
            using var doc = JsonDocument.Parse("{\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":8}}");
            var usage = Http.ReadUsage(doc.RootElement);

            Assert.AreEqual(12, usage.prompt);
            Assert.AreEqual(8, usage.completion);
        }

        [TestMethod]
        public void ReadUsage_MissingUsage_ReturnsNulls()
        {
            using var doc = JsonDocument.Parse("{\"choices\":[]}");
            var usage = Http.ReadUsage(doc.RootElement);

            Assert.IsNull(usage.prompt);
            Assert.IsNull(usage.completion);
        }
    }
}
