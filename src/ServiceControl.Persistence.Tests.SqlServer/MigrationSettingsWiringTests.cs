// ReSharper disable once CheckNamespace
namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Runtime.Loader;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.SqlServer;

class MigrationSettingsWiringTests
{
    [Test]
    public void The_retry_history_depth_is_carried_onto_the_persister()
    {
        var settings = new Settings(transportType: "LearningTransport", persisterType: "SQLServer", errorRetentionPeriod: TimeSpan.FromDays(10))
        {
            // This project references the persister already, so nothing has to be loaded from a persistence manifest.
            AssemblyLoadContextResolver = static _ => AssemblyLoadContext.Default,
            RetryHistoryDepth = 42,
            PersisterSpecificSettings = new SqlServerPersisterSettings
            {
                ConnectionString = "Server=.;Database=no-connection-is-opened-by-this-test",
                BodyStorage = new FileSystemBodyStorageSettings { StoragePath = Path.GetTempPath() }
            }
        };

        PersistenceFactory.Create(settings);

        Assert.That(
            settings.PersisterSpecificSettings.RetryHistoryDepth,
            Is.EqualTo(42),
            "EFCoreMigrationTargetReadiness builds RetryHistoryDepthIsSafeCheck out of this value, so losing the copy refuses every migrating instance over a setting the customer never touched");
    }
}
