using System;
using KingdomTycoon.Services;

namespace KingdomTycoon.Infrastructure.Save
{
    public sealed class SaveService : IAppService
    {
        private readonly string persistentDataPath;
        private readonly string schemaJson;
        private readonly string content5SchemaJson;
        private readonly string content6SchemaJson;

        public SaveService(string persistentDataPath, string schemaJson)
            : this(persistentDataPath, schemaJson, null, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson)
            : this(persistentDataPath, schemaJson, content5SchemaJson, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson, string content6SchemaJson)
        {
            this.persistentDataPath = string.IsNullOrWhiteSpace(persistentDataPath)
                ? throw new ArgumentException("Persistent data path is required.", nameof(persistentDataPath))
                : persistentDataPath;
            this.schemaJson = schemaJson ?? throw new ArgumentNullException(nameof(schemaJson));
            this.content5SchemaJson = content5SchemaJson;
            this.content6SchemaJson = content6SchemaJson;
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
                : content6SchemaJson == null
                    ? new SaveDocumentValidator(schemaJson, content5SchemaJson)
                    : new SaveDocumentValidator(schemaJson, content5SchemaJson, content6SchemaJson);
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
