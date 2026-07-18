using System;
using System.IO;
using System.Text;
using KingdomTycoon.Services;

namespace KingdomTycoon.Infrastructure.Content
{
    public sealed class ContentCatalogService : IAppService
    {
        private readonly string packageDirectory;

        public ContentCatalogService(string packageDirectory)
        {
            this.packageDirectory = string.IsNullOrWhiteSpace(packageDirectory)
                ? throw new ArgumentException("Content package directory is required.", nameof(packageDirectory))
                : packageDirectory;
        }

        public int InitializationOrder => 40;

        public ContentCatalog Catalog { get; private set; }

        public ValidationReport LastReport { get; private set; }

        public void Initialize(ServiceRegistry services)
        {
            Catalog = null;
            LastReport = null;
        }

        public ContentImportResult Load()
        {
            string manifestPath = Path.Combine(packageDirectory, "content_manifest.json");
            string manifestJson = File.ReadAllText(manifestPath, new UTF8Encoding(false, true));
            var importer = new CsvContentImporter();
            ContentImportResult result = importer.Import(
                manifestJson,
                fileName => File.ReadAllText(Path.Combine(packageDirectory, fileName), new UTF8Encoding(false, true)));
            Catalog = result.Catalog;
            LastReport = result.Report;
            return result;
        }

        public void Shutdown()
        {
            Catalog = null;
            LastReport = null;
        }
    }
}
