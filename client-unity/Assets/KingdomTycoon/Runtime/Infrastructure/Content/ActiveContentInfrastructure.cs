using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Content;
using UnityEngine.Networking;

namespace KingdomTycoon.Infrastructure.Content
{
    public sealed class CompileTimeActiveContentVersionProvider : IActiveContentVersionProvider
    {
        public const string P04ContentVersion = "1.0.0-content.2";
        public const string P05ContentVersion = "1.0.0-content.3";
        public const string P06ContentVersion = "1.0.0-content.4";
        public const string P07ContentVersion = "1.0.0-content.5";
        public const string P08ContentVersion = "1.0.0-content.6";
        public const string P09ContentVersion = "1.0.0-content.7";
        public const string P10ContentVersion = "1.0.0-content.8";
        public const string P11ContentVersion = "1.0.0-content.9";
        public const string P12ContentVersion = "1.0.0-content.10";

        public string ActiveContentVersion => P12ContentVersion;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed class DevelopmentActiveContentVersionProvider : IActiveContentVersionProvider
    {
        public DevelopmentActiveContentVersionProvider(string activeContentVersion)
        {
            if (activeContentVersion is not ("1.0.0-content.1" or "1.0.0-content.2" or "1.0.0-content.3" or "1.0.0-content.4" or "1.0.0-content.5" or "1.0.0-content.6" or "1.0.0-content.7" or "1.0.0-content.8" or "1.0.0-content.9" or "1.0.0-content.10"))
            {
                throw new ArgumentException("CONTENT_ACTIVE_VERSION_INVALID", nameof(activeContentVersion));
            }

            ActiveContentVersion = activeContentVersion;
        }

        public string ActiveContentVersion { get; }
    }
#endif

    public sealed class LocalStreamingAssetReader : IStreamingAssetReader
    {
        private readonly string root;

        public LocalStreamingAssetReader(string streamingAssetsPath)
        {
            root = Path.GetFullPath(streamingAssetsPath ?? throw new ArgumentNullException(nameof(streamingAssetsPath)));
        }

        public Task<byte[]> ReadAllBytesAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalized = ContentPathPolicy.ValidateRelativePath(relativePath);
            string path = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_VERSION_INVALID");
            }

            return Task.FromResult(File.ReadAllBytes(path));
        }
    }

    public sealed class AndroidStreamingAssetReader : IStreamingAssetReader
    {
        private readonly string root;

        public AndroidStreamingAssetReader(string streamingAssetsPath)
        {
            root = (streamingAssetsPath ?? throw new ArgumentNullException(nameof(streamingAssetsPath))).TrimEnd('/');
        }

        public Task<byte[]> ReadAllBytesAsync(string relativePath, CancellationToken cancellationToken)
        {
            string normalized = ContentPathPolicy.ValidateRelativePath(relativePath);
            var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            UnityWebRequest request = UnityWebRequest.Get(root + "/" + normalized);
            CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                request.Abort();
                completion.TrySetCanceled(cancellationToken);
            });
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                registration.Dispose();
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        completion.TrySetException(new IOException("CONTENT_ACTIVE_PACKAGE_NOT_FOUND: " + request.error));
                    }
                    else
                    {
                        completion.TrySetResult(request.downloadHandler.data);
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
            return completion.Task;
        }
    }

    internal static class ContentPathPolicy
    {
        private static readonly Regex Segment = new("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant);

        public static string ValidateRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\\') || relativePath.Contains("..", StringComparison.Ordinal) ||
                relativePath.Contains(':') || relativePath.Contains('%') || relativePath.StartsWith('/'))
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_VERSION_INVALID");
            }

            string[] segments = relativePath.Split('/');
            if (segments.Length < 2 || Array.Exists(segments, segment => !Segment.IsMatch(segment)))
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_VERSION_INVALID");
            }

            return string.Join("/", segments);
        }
    }
}
