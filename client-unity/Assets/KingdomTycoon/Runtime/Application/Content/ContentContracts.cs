using System.Threading;
using System.Threading.Tasks;

namespace KingdomTycoon.Application.Content
{
    public interface IActiveContentVersionProvider
    {
        string ActiveContentVersion { get; }
    }

    public interface IStreamingAssetReader
    {
        Task<byte[]> ReadAllBytesAsync(string relativePath, CancellationToken cancellationToken);
    }
}
