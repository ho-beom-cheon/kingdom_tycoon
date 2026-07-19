using System;
using System.IO;
using System.Linq;
using KingdomTycoon.Application.Profiles;

namespace KingdomTycoon.Infrastructure.Save
{
    public sealed class SingleProfileCreator
    {
        private readonly string savesRoot;
        private readonly AtomicSaveRepository canonicalRepository;
        private readonly SaveDocumentValidator validator;

        public SingleProfileCreator(string persistentDataPath, AtomicSaveRepository canonicalRepository, SaveDocumentValidator validator)
        {
            savesRoot = Path.Combine(Path.GetFullPath(persistentDataPath ?? throw new ArgumentNullException(nameof(persistentDataPath))), "saves");
            this.canonicalRepository = canonicalRepository ?? throw new ArgumentNullException(nameof(canonicalRepository));
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public SaveLoadResult CreateOrResume(INewGameFactory factory, DateTimeOffset now)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            Directory.CreateDirectory(savesRoot);
            DirectoryInfo[] staging = new DirectoryInfo(savesRoot).EnumerateDirectories(".creating.*", SearchOption.TopDirectoryOnly).OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
            if (staging.Length > 1) return SaveLoadResult.Failed("SAVE_CREATE_STAGING_AMBIGUOUS", null);

            string profileId;
            string stagingRoot;
            if (staging.Length == 1)
            {
                profileId = staging[0].Name.Substring(".creating.".Length);
                if (!Guid.TryParseExact(profileId, "D", out _) || profileId.Length != 36 || profileId[14] != '7')
                    return SaveLoadResult.Failed("SAVE_CREATE_STAGING_INVALID", null);
                stagingRoot = staging[0].FullName;
                SaveLoadResult staged = AtomicSaveRepository.CreateForSavesRoot(stagingRoot, validator).Load(profileId);
                if (!staged.Success) return SaveLoadResult.Failed("SAVE_CREATE_STAGING_INVALID", staged.Report);
            }
            else
            {
                string saveId = UuidV7.NewString(now);
                profileId = UuidV7.NewString(now);
                stagingRoot = Path.Combine(savesRoot, ".creating." + profileId);
                Directory.CreateDirectory(stagingRoot);
                AtomicSaveRepository stagingRepository = AtomicSaveRepository.CreateForSavesRoot(stagingRoot, validator);
                SaveWriteResult written = stagingRepository.Save(profileId, factory.CreateDraft(saveId, profileId, now), 0, now);
                if (!written.Success) return SaveLoadResult.Failed(written.ErrorCode ?? "SAVE_CREATE_FAILED", written.Report);
            }

            string stagedProfile = Path.Combine(stagingRoot, profileId);
            string canonicalProfile = Path.Combine(savesRoot, profileId);
            if (Directory.Exists(canonicalProfile)) return SaveLoadResult.Failed("SAVE_CREATE_PROFILE_CONFLICT", null);
            Directory.Move(stagedProfile, canonicalProfile);
            Directory.Delete(stagingRoot, false);
            return canonicalRepository.Load(profileId);
        }
    }
}
