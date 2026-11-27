namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Raven.Client.Documents.Smuggler;
using ServiceControl.MessageFailures;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;

abstract class RavenMigrationSourceTestBase : RavenPersistenceTestBase
{
    static readonly SettingsRootNamespace SettingsRoot = new("ServiceControl");
    static readonly object SettingsGate = new();

    protected string ThroughputDatabaseName => ((RavenPersisterSettings)PersistenceSettings).ThroughputDatabaseName;

    protected async Task<IMigrationSource> OpenMigrationSource()
    {
        var settings = (RavenPersisterSettings)PersistenceSettings;
        (string Name, string Value)[] variables =
        [
            ("SERVICECONTROL_RAVENDB_CONNECTIONSTRING", settings.ConnectionString),
            ("SERVICECONTROL_RAVENDB_DATABASENAME", settings.DatabaseName),
            ("SERVICECONTROL_ERRORRETENTIONPERIOD", settings.ErrorRetentionPeriod.ToString()),
            ("LICENSINGCOMPONENT_RAVENDB_THROUGHPUTDATABASENAME", settings.ThroughputDatabaseName)
        ];

        IMigrationSource source;

        // Environment variables are process wide and these tests run in parallel, so without the gate a source
        // reads whichever test set them last and opens another test's database.
        lock (SettingsGate)
        {
            try
            {
                foreach (var (name, value) in variables)
                {
                    Environment.SetEnvironmentVariable(name, value);
                }

                source = new RavenPersistenceConfiguration().CreateSource(SettingsRoot);
            }
            finally
            {
                foreach (var (name, _) in variables)
                {
                    Environment.SetEnvironmentVariable(name, null);
                }
            }
        }

        await source.Open();

        return source;
    }

    // RavenDB assigns last-modified from its own clock, but a smuggler import keeps the one carried in the document.
    protected async Task<FailedMessage> SeedFailedMessage(FailedMessage failedMessage, DateTime lastModified)
    {
        await SeedFailedMessage(failedMessage);

        JObject document;
        using (var session = DocumentStore.OpenAsyncSession())
        {
            var stored = await session.LoadAsync<FailedMessage>(failedMessage.Id);
            document = JObject.FromObject(stored);
            var metadata = session.Advanced.GetMetadataFor(stored);
            document["@metadata"] = new JObject
            {
                ["@id"] = failedMessage.Id,
                ["@collection"] = (string)metadata["@collection"],
                ["Raven-Clr-Type"] = (string)metadata["Raven-Clr-Type"],
                ["@last-modified"] = lastModified.ToUniversalTime()
            };
        }

        using var dump = new MemoryStream();
        using (var gzip = new GZipStream(dump, CompressionMode.Compress, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(new JObject { ["Docs"] = new JArray(document) }.ToString(Formatting.None)));
        }

        dump.Position = 0;
        var import = await DocumentStore.Smuggler.ImportAsync(new DatabaseSmugglerImportOptions(), dump, TestContext.CurrentContext.CancellationToken);
        await import.WaitForCompletionAsync();

        return failedMessage;
    }

    protected static async Task<List<MigrationBatch>> CollectBatches(IMigrationSource source, MigrationCategory category, string resumeAfter = null, int batchSize = 100)
    {
        var batches = new List<MigrationBatch>();

        await foreach (var batch in source.Read(category, resumeAfter, batchSize, TestContext.CurrentContext.CancellationToken))
        {
            batches.Add(batch);
        }

        return batches;
    }
}
