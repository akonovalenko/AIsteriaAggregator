using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aisteria.Models;

namespace Aisteria.Providers
{
    public interface IAIProvider
    {
        string Name { get; }
        bool SupportsImages { get; }
        Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default);

        // Default implementation: delegate verbose detection and diagnostics logging to Logger.
        bool IsVerboseEnabled()
        {
            return Aisteria.Logger.Instance.IsVerboseEnabled(Name);
        }

        void LogDiagnostics(string text)
        {
            Aisteria.Logger.Instance.LogDiagnostics(this, text);
        }
    }
}
