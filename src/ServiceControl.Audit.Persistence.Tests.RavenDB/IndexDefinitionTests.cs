namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;
using Persistence.RavenDB.Indexes;
using Raven.Client.Documents.Indexes;
using ServiceControl.RavenDB;
using ServiceControl.SagaAudit;

[TestFixture]
class IndexDefinitionTests : CleanDatabaseFixture
{
    // A definition that differs from the one on the server makes RavenDB rebuild the index side by side, which takes
    // days for the full-text index of a large audit database. Do not approve a change without a plan for the rebuild.
    [Test]
    public async Task VerifyGeneratedDefinitions()
    {
        AbstractIndexCreationTask[] indexes =
        [
            new FailedAuditImportIndex(),
            new MessagesViewIndex(),
            new MessagesViewIndexWithFullTextSearch(),
            new SagaDetailsIndex()
        ];

        await IndexDeployment.CreateIndexesAsync(indexes, Store, TestTimeoutCancellationToken);

        var description = new StringBuilder();

        foreach (var index in indexes.OrderBy(i => i.IndexName, StringComparer.Ordinal))
        {
            index.Conventions = Store.Conventions;
            Describe(description, index.IndexName, index.CreateIndexDefinition());
        }

        Approver.Verify(description.ToString().ReplaceLineEndings());
    }

    static void Describe(StringBuilder description, string indexName, IndexDefinition definition)
    {
        description.AppendLine($"Index: {indexName}");
        description.AppendLine($"Type: {definition.Type}");
        description.AppendLine($"SourceType: {definition.SourceType}");

        description.AppendLine("Maps:");
        foreach (var map in definition.Maps.OrderBy(m => m, StringComparer.Ordinal))
        {
            description.AppendLine($"  {map}");
        }

        description.AppendLine($"Reduce: {definition.Reduce ?? "(none)"}");

        description.AppendLine("Fields:");
        foreach (var (name, options) in definition.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            description.AppendLine($"  {name}: Storage={options.Storage}, Indexing={options.Indexing}, TermVector={options.TermVector}, Suggestions={options.Suggestions}, Analyzer={options.Analyzer ?? "(none)"}, HasSpatial={options.Spatial != null}");
        }

        description.AppendLine("Configuration:");
        foreach (var (key, value) in definition.Configuration.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            description.AppendLine($"  {key}={value}");
        }

        description.AppendLine("AdditionalSources:");
        foreach (var (name, source) in definition.AdditionalSources.OrderBy(s => s.Key, StringComparer.Ordinal))
        {
            description.AppendLine($"  {name}: {source}");
        }

        description.AppendLine($"AdditionalAssemblies: {definition.AdditionalAssemblies.Count}");
        description.AppendLine($"OutputReduceToCollection: {definition.OutputReduceToCollection ?? "(none)"}");
        description.AppendLine();
    }
}
