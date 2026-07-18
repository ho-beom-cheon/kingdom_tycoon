using System;
using KingdomTycoon.Services;

namespace KingdomTycoon.Infrastructure.Save
{
    public sealed class SaveService : IAppService
    {
        private readonly string persistentDataPath;
        private readonly string schemaJson;
        private readonly string content5SchemaJson;

        public SaveService(string persistentDataPath, string schemaJson)
            : this(persistentDataPath, schemaJson, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson)
        {
            this.persistentDataPath = string.IsNullOrWhiteSpace(persistentDataPath)
                ? throw new ArgumentException("Persistent data path is required.", nameof(persistentDataPath))
                : persistentDataPath;
            this.schemaJson = schemaJson ?? throw new ArgumentNullException(nameof(schemaJson));
            this.content5SchemaJson = content5SchemaJson;
        }

        public int InitializationOrder => 30;

        public string PersistentDataPath => persistentDataPath;

        public SaveDocumentValidator Validator { get; private set; }

        public ISaveRepository Repository { get; private set; }

        public SaveMigrationRegistry Migrations { get; private set; }

        public void Initialize(ServiceRegistry services)
        {
            Validator = content5SchemaJson == null
                ? new SaveDocumentValidator(schemaJson)
                : new SaveDocumentValidator(schemaJson, content5SchemaJson);
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
