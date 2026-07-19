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
        private readonly string content7SchemaJson;
        private readonly string content8SchemaJson;
        private readonly string content9SchemaJson;
        private readonly string content10SchemaJson;
        private readonly string content11SchemaJson;

        public SaveService(string persistentDataPath, string schemaJson)
            : this(persistentDataPath, schemaJson, null, null, null, null, null, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson)
            : this(persistentDataPath, schemaJson, content5SchemaJson, null, null, null, null, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson, string content6SchemaJson)
            : this(persistentDataPath, schemaJson, content5SchemaJson, content6SchemaJson, null, null, null, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson)
            : this(persistentDataPath, schemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, null, null, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson)
            : this(persistentDataPath, schemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, content8SchemaJson, null, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson, string content9SchemaJson)
            : this(persistentDataPath, schemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, content8SchemaJson, content9SchemaJson, null)
        {
        }

        public SaveService(string persistentDataPath, string schemaJson, string content5SchemaJson, string content6SchemaJson, string content7SchemaJson, string content8SchemaJson, string content9SchemaJson, string content10SchemaJson, string content11SchemaJson = null)
        {
            this.persistentDataPath = string.IsNullOrWhiteSpace(persistentDataPath)
                ? throw new ArgumentException("Persistent data path is required.", nameof(persistentDataPath))
                : persistentDataPath;
            this.schemaJson = schemaJson ?? throw new ArgumentNullException(nameof(schemaJson));
            this.content5SchemaJson = content5SchemaJson;
            this.content6SchemaJson = content6SchemaJson;
            this.content7SchemaJson = content7SchemaJson;
            this.content8SchemaJson = content8SchemaJson;
            this.content9SchemaJson = content9SchemaJson;
            this.content10SchemaJson = content10SchemaJson;
            this.content11SchemaJson = content11SchemaJson;
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
                    : new SaveDocumentValidator(schemaJson, content5SchemaJson, content6SchemaJson, content7SchemaJson, content8SchemaJson, content9SchemaJson, content10SchemaJson, content11SchemaJson);
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
