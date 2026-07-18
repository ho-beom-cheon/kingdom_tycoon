using System;
using KingdomTycoon.Services;

namespace KingdomTycoon.Infrastructure.Save
{
    public sealed class SaveService : IAppService
    {
        private readonly string persistentDataPath;
        private readonly string schemaJson;

        public SaveService(string persistentDataPath, string schemaJson)
        {
            this.persistentDataPath = string.IsNullOrWhiteSpace(persistentDataPath)
                ? throw new ArgumentException("Persistent data path is required.", nameof(persistentDataPath))
                : persistentDataPath;
            this.schemaJson = schemaJson ?? throw new ArgumentNullException(nameof(schemaJson));
        }

        public int InitializationOrder => 30;

        public SaveDocumentValidator Validator { get; private set; }

        public ISaveRepository Repository { get; private set; }

        public SaveMigrationRegistry Migrations { get; private set; }

        public void Initialize(ServiceRegistry services)
        {
            Validator = new SaveDocumentValidator(schemaJson);
            Migrations = new SaveMigrationRegistry(1);
            Repository = new AtomicSaveRepository(persistentDataPath, Validator);
        }

        public void Shutdown()
        {
            Repository = null;
            Migrations = null;
            Validator = null;
        }
    }
}
