using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using KingdomTycoon.Application.Content;
using KingdomTycoon.Services;
using Newtonsoft.Json.Linq;

namespace KingdomTycoon.Infrastructure.Content
{
    public sealed class ContentCatalogService : IAppService
    {
        private static readonly Regex ContentVersionPattern = new("^[0-9]+\\.[0-9]+\\.[0-9]+-content\\.[1-9][0-9]*$", RegexOptions.CultureInvariant);
        private readonly IStreamingAssetReader reader;
        private readonly IActiveContentVersionProvider versionProvider;

        public ContentCatalogService(string streamingAssetsPath)
            : this(new LocalStreamingAssetReader(streamingAssetsPath), new CompileTimeActiveContentVersionProvider())
        {
        }

        public ContentCatalogService(IStreamingAssetReader reader, IActiveContentVersionProvider versionProvider)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
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
            LoadActiveAsync(CancellationToken.None).GetAwaiter().GetResult();
            return new ContentImportResult(Catalog, LastReport);
        }

        public async Task<ContentCatalog> LoadActiveAsync(CancellationToken cancellationToken)
        {
            string version = versionProvider.ActiveContentVersion;
            if (!ContentVersionPattern.IsMatch(version ?? string.Empty))
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_VERSION_INVALID");
            }

            string root = "Content/" + version + "/";
            string manifestJson;
            try
            {
                byte[] manifestBytes = await reader.ReadAllBytesAsync(root + "content_manifest.json", cancellationToken);
                _ = await reader.ReadAllBytesAsync(root + "content_manifest.schema.json", cancellationToken);
                manifestJson = new UTF8Encoding(false, true).GetString(manifestBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_PACKAGE_NOT_FOUND", exception);
            }

            JObject manifest = JObject.Parse(manifestJson);
            if (!string.Equals(manifest.Value<string>("contentVersion"), version, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_MANIFEST_VERSION_MISMATCH");
            }

            var tables = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JObject descriptor in manifest["tables"]!.Children<JObject>())
            {
                string fileName = descriptor.Value<string>("file");
                byte[] bytes = await reader.ReadAllBytesAsync(root + fileName, cancellationToken);
                tables.Add(fileName, new UTF8Encoding(false, true).GetString(bytes));
            }

            ContentImportResult result = new CsvContentImporter().Import(manifestJson, fileName => tables[fileName]);
            LastReport = result.Report;
            Catalog = result.Catalog;
            if (!result.IsValid)
            {
                throw new InvalidOperationException("CONTENT_ACTIVE_PACKAGE_VALIDATION_FAILED: " + string.Join("; ", result.Report.Issues));
            }

            return Catalog;
        }

        public void Shutdown()
        {
            Catalog = null;
            LastReport = null;
        }
    }
}
