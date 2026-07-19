using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Profiles;

namespace KingdomTycoon.Infrastructure.Save
{
    public sealed class LocalProfileLocator : IProfileLocator
    {
        private static readonly Regex UuidV7 = new("^[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.CultureInvariant);
        private static readonly string[] CandidateNames = { "save.json", "save.tmp", "save.bak1.json", "save.bak2.json", "save.bak3.json" };
        private readonly string savesRoot;
        private readonly ISaveRepository repository;

        public LocalProfileLocator(string persistentDataPath, ISaveRepository repository)
        {
            savesRoot = Path.Combine(Path.GetFullPath(persistentDataPath ?? throw new ArgumentNullException(nameof(persistentDataPath))), "saves");
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public Task<ProfileLocateResult> LocateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(savesRoot);
            var valid = new List<string>();
            var invalid = new List<string>();
            IEnumerable<DirectoryInfo> directories;
            try
            {
                directories = new DirectoryInfo(savesRoot).EnumerateDirectories().OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException("SAVE_PROFILE_DISCOVERY_FAILED", exception);
            }

            foreach (DirectoryInfo directory in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (directory.Name.StartsWith(".creating.", StringComparison.Ordinal))
                {
                    continue;
                }
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    invalid.Add(directory.Name + ":SAVE_PROFILE_LINK_FORBIDDEN");
                    continue;
                }
                if (!UuidV7.IsMatch(directory.Name))
                {
                    invalid.Add(directory.Name + ":SAVE_PROFILE_DIRECTORY_NAME_INVALID");
                    continue;
                }
                if (!CandidateNames.Any(name => File.Exists(Path.Combine(directory.FullName, name))))
                {
                    invalid.Add(directory.Name + ":SAVE_PROFILE_NO_SAVE_CANDIDATE");
                    continue;
                }

                SaveLoadResult load = repository.Load(directory.Name);
                if (load.Success && string.Equals(load.Document.Value<string>("profileId"), directory.Name, StringComparison.Ordinal))
                {
                    valid.Add(directory.Name);
                }
                else
                {
                    invalid.Add(directory.Name + ":" + (load.ErrorCode ?? "SAVE_VALIDATION_FAILED"));
                }
            }

            ProfileLocateKind kind = valid.Count switch
            {
                0 => ProfileLocateKind.NONE,
                1 => ProfileLocateKind.ONE,
                _ => ProfileLocateKind.AMBIGUOUS
            };
            return Task.FromResult(new ProfileLocateResult(kind, valid.Count == 1 ? valid[0] : null, valid, invalid));
        }
    }
}
