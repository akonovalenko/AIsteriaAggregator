namespace Aisteria
{
    public interface ILogger
    {
        // Append text to the diagnostics log and return the path written to.
        string Log(string text);

        // Return the latest diagnostics log file path or empty if none.
        string GetLatestLog();

        // Check whether verbose logging is enabled for the given provider name (or globally).
        bool IsVerboseEnabled(string providerName);

        // Write provider-scoped diagnostics (implementation should mask secrets).
        void LogDiagnostics(Providers.IAIProvider provider, string text);

        // Log request/response details for an HTTP request made by a provider. Implementations
        // should mask sensitive headers and optionally truncate large bodies.
        void LogRequestResponse(Providers.IAIProvider provider, System.Net.Http.HttpRequestMessage request, System.Net.Http.HttpResponseMessage response, string responseBody);
    }
}
