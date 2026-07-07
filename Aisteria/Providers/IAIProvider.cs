using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aisteria.Providers
{
    public interface IAIProvider
    {
        string Name { get; }
        bool SupportsImages { get; }
        Task<AiResponse> AskAsync(string prompt, IReadOnlyList<ImageInput> images = null, CancellationToken ct = default);
    }
}
