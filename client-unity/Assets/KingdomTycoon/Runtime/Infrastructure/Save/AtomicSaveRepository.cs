using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using KingdomTycoon.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Save
{
    public interface ISaveRepository
    {
        SaveLoadResult Load(string profileId);

        SaveWriteResult Save(string profileId, JObject document, long expectedRevision, DateTimeOffset savedAtUtc);
    }

    public sealed class SaveLoadResult
    {
        private SaveLoadResult(bool success, JObject document, string source, bool recovered, string errorCode, ValidationReport report)
        {
            Success = success;
            Document = document;
            Source = source;
            Recovered = recovered;
            ErrorCode = errorCode;
            Report = report ?? new ValidationReport();
        }

        public bool Success { get; }

        public JObject Document { get; }

        public string Source { get; }

        public bool Recovered { get; }

        public string ErrorCode { get; }

        public ValidationReport Report { get; }

        public static SaveLoadResult Loaded(JObject document, string source, bool recovered, ValidationReport report = null)
        {
            return new SaveLoadResult(true, document, source, recovered, null, report);
        }

        public static SaveLoadResult Failed(string errorCode, ValidationReport report)
        {
            return new SaveLoadResult(false, null, null, false, errorCode, report);
        }
    }

    public sealed class SaveWriteResult
    {
        private SaveWriteResult(bool success, JObject document, string errorCode, ValidationReport report)
        {
            Success = success;
            Document = document;
            ErrorCode = errorCode;
            Report = report ?? new ValidationReport();
        }

        public bool Success { get; }

        public JObject Document { get; }

        public string ErrorCode { get; }

        public ValidationReport Report { get; }

        public static SaveWriteResult Written(JObject document)
        {
            return new SaveWriteResult(true, document, null, null);
        }

        public static SaveWriteResult Failed(string errorCode, ValidationReport report = null)
        {
            return new SaveWriteResult(false, null, errorCode, report);
        }
    }

    public sealed class AtomicSaveRepository : ISaveRepository
    {
        private const long MaximumSaveBytes = 16L * 1024L * 1024L;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProcessLocks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly UTF8Encoding Utf8WithoutBom = new(false, true);

        private readonly string savesRoot;
        private readonly SaveDocumentValidator validator;
        private readonly TimeSpan lockTimeout;

        public AtomicSaveRepository(string persistentDataPath, SaveDocumentValidator validator, TimeSpan? lockTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(persistentDataPath))
            {
                throw new ArgumentException("Persistent data path is required.", nameof(persistentDataPath));
            }

            savesRoot = Path.Combine(Path.GetFullPath(persistentDataPath), "saves");
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
            this.lockTimeout = lockTimeout ?? TimeSpan.FromSeconds(5);
        }

        public SaveLoadResult Load(string profileId)
        {
            string profileDirectory;
            try
            {
                profileDirectory = GetProfileDirectory(profileId);
            }
            catch (Exception)
            {
                return SaveLoadResult.Failed("SAVE_PROFILE_ID_INVALID", null);
            }

            Directory.CreateDirectory(profileDirectory);
            SemaphoreSlim processLock = ProcessLocks.GetOrAdd(profileDirectory, _ => new SemaphoreSlim(1, 1));
            if (!processLock.Wait(lockTimeout))
            {
                return SaveLoadResult.Failed("SAVE_LOCK_TIMEOUT", null);
            }

            try
            {
                using FileStream lockFile = TryAcquireFileLock(profileDirectory);
                if (lockFile == null)
                {
                    return SaveLoadResult.Failed("SAVE_LOCK_TIMEOUT", null);
                }

                return LoadUnderLock(profileDirectory);
            }
            catch (UnauthorizedAccessException)
            {
                return SaveLoadResult.Failed("SAVE_IO_ACCESS_DENIED", null);
            }
            catch (IOException)
            {
                return SaveLoadResult.Failed("SAVE_IO_ERROR", null);
            }
            catch (PlatformNotSupportedException)
            {
                return SaveLoadResult.Failed("SAVE_ATOMIC_REPLACE_UNSUPPORTED", null);
            }
            finally
            {
                processLock.Release();
            }
        }

        public SaveWriteResult Save(string profileId, JObject document, long expectedRevision, DateTimeOffset savedAtUtc)
        {
            string profileDirectory;
            try
            {
                profileDirectory = GetProfileDirectory(profileId);
            }
            catch (Exception)
            {
                return SaveWriteResult.Failed("SAVE_PROFILE_ID_INVALID");
            }

            if (!string.Equals(document?.Value<string>("profileId"), profileId, StringComparison.OrdinalIgnoreCase))
            {
                return SaveWriteResult.Failed("SAVE_PROFILE_ID_MISMATCH");
            }

            Directory.CreateDirectory(profileDirectory);
            SemaphoreSlim processLock = ProcessLocks.GetOrAdd(profileDirectory, _ => new SemaphoreSlim(1, 1));
            if (!processLock.Wait(lockTimeout))
            {
                return SaveWriteResult.Failed("SAVE_LOCK_TIMEOUT");
            }

            try
            {
                using FileStream lockFile = TryAcquireFileLock(profileDirectory);
                if (lockFile == null)
                {
                    return SaveWriteResult.Failed("SAVE_LOCK_TIMEOUT");
                }

                return SaveUnderLock(profileDirectory, document, expectedRevision, savedAtUtc);
            }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("SAVE_REVISION_CONFLICT", StringComparison.Ordinal))
            {
                return SaveWriteResult.Failed("SAVE_REVISION_CONFLICT");
            }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("SAVE_SPLIT_BRAIN", StringComparison.Ordinal))
            {
                return SaveWriteResult.Failed("SAVE_SPLIT_BRAIN");
            }
            catch (UnauthorizedAccessException)
            {
                return SaveWriteResult.Failed("SAVE_IO_ACCESS_DENIED");
            }
            catch (IOException)
            {
                return SaveWriteResult.Failed("SAVE_IO_ERROR");
            }
            catch (PlatformNotSupportedException)
            {
                return SaveWriteResult.Failed("SAVE_ATOMIC_REPLACE_UNSUPPORTED");
            }
            finally
            {
                processLock.Release();
            }
        }

        private SaveLoadResult LoadUnderLock(string profileDirectory)
        {
            IReadOnlyList<Candidate> candidates = GetCandidatePaths(profileDirectory);
            var validCandidates = new List<ValidatedCandidate>();
            var aggregateReport = new ValidationReport();

            foreach (Candidate candidate in candidates)
            {
                if (!File.Exists(candidate.Path))
                {
                    continue;
                }

                SaveValidationResult validation = ReadAndValidate(candidate.Path);
                aggregateReport.Merge(validation.Report);
                if (!validation.IsValid)
                {
                    Quarantine(candidate.Path);
                    continue;
                }

                validCandidates.Add(new ValidatedCandidate(candidate, validation.Document));
            }

            if (validCandidates.Count == 0)
            {
                return SaveLoadResult.Failed("SAVE_NO_VALID_CANDIDATE", aggregateReport);
            }

            foreach (IGrouping<long, ValidatedCandidate> revisionGroup in validCandidates.GroupBy(value => value.Revision))
            {
                if (revisionGroup.Select(value => value.FileHash).Distinct(StringComparer.Ordinal).Skip(1).Any())
                {
                    aggregateReport.AddError(
                        "SAVE_SPLIT_BRAIN",
                        "save-directory",
                        "/revision",
                        $"Revision {revisionGroup.Key.ToString(CultureInfo.InvariantCulture)} has conflicting file hashes.");
                    return SaveLoadResult.Failed("SAVE_SPLIT_BRAIN", aggregateReport);
                }
            }

            ValidatedCandidate selected = validCandidates
                .OrderByDescending(candidate => candidate.Revision)
                .ThenByDescending(candidate => candidate.SavedAtUtc)
                .ThenBy(candidate => candidate.Candidate.Priority)
                .First();

            if (selected.Candidate.Priority == 0)
            {
                return SaveLoadResult.Loaded(selected.Document, selected.Candidate.Name, false, aggregateReport);
            }

            SaveWriteResult recovery = SaveUnderLock(
                profileDirectory,
                selected.Document,
                selected.Revision,
                DateTimeOffset.UtcNow);
            if (!recovery.Success)
            {
                return SaveLoadResult.Failed(recovery.ErrorCode ?? "SAVE_RECOVERY_FAILED", recovery.Report);
            }

            return SaveLoadResult.Loaded(recovery.Document, selected.Candidate.Name, true, aggregateReport);
        }

        private SaveWriteResult SaveUnderLock(
            string profileDirectory,
            JObject document,
            long expectedRevision,
            DateTimeOffset savedAtUtc)
        {
            VerifyDiskRevision(profileDirectory, expectedRevision);
            JObject prepared = validator.PrepareForCommit(document, expectedRevision, savedAtUtc);
            ValidationReport preparedReport = validator.Validate(prepared, "memory:prepared-save");
            if (!preparedReport.IsValid)
            {
                return SaveWriteResult.Failed("SAVE_VALIDATION_FAILED", preparedReport);
            }

            string temporaryPath = Path.Combine(profileDirectory, "save.tmp");
            WriteThrough(temporaryPath, prepared.ToString(Formatting.None));
            SaveValidationResult temporaryValidation = ReadAndValidate(temporaryPath);
            if (!temporaryValidation.IsValid)
            {
                Quarantine(temporaryPath);
                return SaveWriteResult.Failed("SAVE_TEMP_VERIFY_FAILED", temporaryValidation.Report);
            }

            string activePath = Path.Combine(profileDirectory, "save.json");
            RotateBackups(profileDirectory, activePath);
            if (File.Exists(activePath))
            {
                File.Replace(temporaryPath, activePath, null, true);
            }
            else
            {
                File.Move(temporaryPath, activePath);
            }

            SaveValidationResult activeValidation = ReadAndValidate(activePath);
            return activeValidation.IsValid
                ? SaveWriteResult.Written(activeValidation.Document)
                : SaveWriteResult.Failed("SAVE_ACTIVE_VERIFY_FAILED", activeValidation.Report);
        }

        private void VerifyDiskRevision(string profileDirectory, long expectedRevision)
        {
            var validCandidates = new List<ValidatedCandidate>();
            foreach (Candidate candidate in GetCandidatePaths(profileDirectory))
            {
                if (!File.Exists(candidate.Path))
                {
                    continue;
                }

                SaveValidationResult validation = ReadAndValidate(candidate.Path);
                if (validation.IsValid)
                {
                    validCandidates.Add(new ValidatedCandidate(candidate, validation.Document));
                }
            }

            if (validCandidates.Count == 0)
            {
                if (expectedRevision != 0)
                {
                    throw new InvalidOperationException("SAVE_REVISION_CONFLICT: no valid on-disk revision exists.");
                }

                return;
            }

            long highestRevision = validCandidates.Max(candidate => candidate.Revision);
            if (validCandidates
                .Where(candidate => candidate.Revision == highestRevision)
                .Select(candidate => candidate.FileHash)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any())
            {
                throw new InvalidOperationException("SAVE_SPLIT_BRAIN: highest on-disk revision has conflicting hashes.");
            }

            if (highestRevision != expectedRevision)
            {
                throw new InvalidOperationException(
                    $"SAVE_REVISION_CONFLICT: expected {expectedRevision.ToString(CultureInfo.InvariantCulture)}, " +
                    $"but disk revision is {highestRevision.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        private void RotateBackups(string profileDirectory, string activePath)
        {
            string backup1 = Path.Combine(profileDirectory, "save.bak1.json");
            string backup2 = Path.Combine(profileDirectory, "save.bak2.json");
            string backup3 = Path.Combine(profileDirectory, "save.bak3.json");
            CopyVerified(backup2, backup3);
            CopyVerified(backup1, backup2);
            CopyVerified(activePath, backup1);
        }

        private void CopyVerified(string source, string destination)
        {
            if (!File.Exists(source))
            {
                return;
            }

            SaveValidationResult validation = ReadAndValidate(source);
            if (!validation.IsValid)
            {
                Quarantine(source);
                return;
            }

            File.Copy(source, destination, true);
            if (!ReadAndValidate(destination).IsValid)
            {
                throw new IOException("Backup verification failed.");
            }
        }

        private SaveValidationResult ReadAndValidate(string path)
        {
            var report = new ValidationReport();
            var info = new FileInfo(path);
            if (info.Length < 1 || info.Length > MaximumSaveBytes)
            {
                report.AddError("SAVE_FILE_SIZE_INVALID", Path.GetFileName(path), "/", "Save file must be between 1 byte and 16 MiB.");
                return new SaveValidationResult(null, report);
            }

            string json = File.ReadAllText(path, Utf8WithoutBom);
            return validator.ParseAndValidate(json, Path.GetFileName(path));
        }

        private FileStream TryAcquireFileLock(string profileDirectory)
        {
            string lockPath = Path.Combine(profileDirectory, "save.lock");
            DateTimeOffset deadline = DateTimeOffset.UtcNow + lockTimeout;
            int delayMilliseconds = 50;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    var stream = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough);
                    using Process process = Process.GetCurrentProcess();
                    string metadata = new JObject
                    {
                        ["processId"] = process.Id,
                        ["acquiredAtUtc"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
                        ["ownerId"] = UuidV7.NewString(DateTimeOffset.UtcNow)
                    }.ToString(Formatting.None);
                    byte[] bytes = Utf8WithoutBom.GetBytes(metadata);
                    stream.SetLength(0);
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                    return stream;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMilliseconds);
                    delayMilliseconds = Math.Min(delayMilliseconds * 2, 500);
                }
            }

            return null;
        }

        private static void WriteThrough(string path, string contents)
        {
            byte[] bytes = Utf8WithoutBom.GetBytes(contents);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static void Quarantine(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            string suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
            string destination = path + ".corrupt-" + suffix;
            try
            {
                File.Move(path, destination);
            }
            catch (IOException)
            {
                // The caller still treats the candidate as invalid; quarantine diagnostics are best effort.
            }
        }

        private string GetProfileDirectory(string profileId)
        {
            if (!Guid.TryParseExact(profileId, "D", out Guid parsed))
            {
                throw new ArgumentException("Profile ID must be a canonical UUID.", nameof(profileId));
            }

            string canonical = parsed.ToString("D");
            string directory = Path.GetFullPath(Path.Combine(savesRoot, canonical));
            string rootWithSeparator = savesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!directory.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Profile path escaped the save root.");
            }

            return directory;
        }

        private static IReadOnlyList<Candidate> GetCandidatePaths(string profileDirectory)
        {
            return new[]
            {
                new Candidate("active", Path.Combine(profileDirectory, "save.json"), 0),
                new Candidate("tmp", Path.Combine(profileDirectory, "save.tmp"), 1),
                new Candidate("bak1", Path.Combine(profileDirectory, "save.bak1.json"), 2),
                new Candidate("bak2", Path.Combine(profileDirectory, "save.bak2.json"), 3),
                new Candidate("bak3", Path.Combine(profileDirectory, "save.bak3.json"), 4)
            };
        }

        private sealed class Candidate
        {
            public Candidate(string name, string path, int priority)
            {
                Name = name;
                Path = path;
                Priority = priority;
            }

            public string Name { get; }

            public string Path { get; }

            public int Priority { get; }
        }

        private sealed class ValidatedCandidate
        {
            public ValidatedCandidate(Candidate candidate, JObject document)
            {
                Candidate = candidate;
                Document = document;
            }

            public Candidate Candidate { get; }

            public JObject Document { get; }

            public long Revision => Document.Value<long>("revision");

            public string FileHash => Document["integrity"].Value<string>("fileSha256");

            public DateTimeOffset SavedAtUtc => DateTimeOffset.ParseExact(
                Document.Value<string>("savedAtUtc"),
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }
    }
}
