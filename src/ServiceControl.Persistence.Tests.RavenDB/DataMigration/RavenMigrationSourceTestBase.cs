namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Configuration;
using ServiceControl.Persistence.DataMigration;
using ServiceControl.Persistence.RavenDB;

abstract class RavenMigrationSourceTestBase : RavenPersistenceTestBase
{
    static readonly SettingsRootNamespace SettingsRoot = new("ServiceControl");
    static readonly object SettingsGate = new();

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
