using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Save
{
    public interface ISaveMigration
    {
        int FromVersion { get; }

        int ToVersion { get; }

        JObject Migrate(JObject source);
    }

    public sealed class SaveMigrationRegistry
    {
        private readonly SortedDictionary<int, ISaveMigration> migrations = new();

        public int CurrentVersion { get; }

        public SaveMigrationRegistry(int currentVersion)
        {
            if (currentVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(currentVersion));
            }

            CurrentVersion = currentVersion;
        }

        public void Register(ISaveMigration migration)
        {
            if (migration == null)
            {
                throw new ArgumentNullException(nameof(migration));
            }

            if (migration.ToVersion != migration.FromVersion + 1)
            {
                throw new InvalidOperationException("Save migrations must advance exactly one version.");
            }

            if (!migrations.TryAdd(migration.FromVersion, migration))
            {
                throw new InvalidOperationException($"A migration from version {migration.FromVersion} is already registered.");
            }
        }

        public JObject MigrateToCurrent(JObject source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var result = (JObject)source.DeepClone();
            int version = result.Value<int>("saveVersion");
            if (version > CurrentVersion)
            {
                throw new InvalidOperationException($"SAVE_VERSION_FUTURE: version {version} is newer than {CurrentVersion}.");
            }

            while (version < CurrentVersion)
            {
                if (!migrations.TryGetValue(version, out ISaveMigration migration))
                {
                    throw new InvalidOperationException($"SAVE_MIGRATION_MISSING: no migration from version {version}.");
                }

                result = migration.Migrate(result)
                    ?? throw new InvalidOperationException($"Migration from version {version} returned null.");
                int migratedVersion = result.Value<int>("saveVersion");
                if (migratedVersion != migration.ToVersion)
                {
                    throw new InvalidOperationException(
                        $"SAVE_MIGRATION_VERSION_INVALID: expected {migration.ToVersion}, got {migratedVersion}.");
                }

                version = migratedVersion;
            }

            return result;
        }
    }
}
