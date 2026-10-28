namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation;
using ServiceControl.Persistence.EFCore.Infrastructure;

[TestFixture]
[NonParallelizable]
class EFEnvironmentDataProviderConfigurationTests
{
    [TearDown]
    public void TearDown()
    {
        foreach (var variable in Variables)
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Test]
    public async Task Should_report_defaults_when_nothing_is_configured()
    {
        var data = await GetData(new TestPersisterSettings { ConnectionString = "Host=localhost", BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "/var/bodies" } });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Storage.Schema"], Is.EqualTo("Default"));
            Assert.That(data["Storage.CommandTimeoutSeconds"], Is.EqualTo("Default"));
            Assert.That(data["Storage.QueryTimeoutSeconds"], Is.EqualTo("Default"));
            Assert.That(data["Storage.SubscriptionCacheSeconds"], Is.EqualTo("Default"));
            Assert.That(data["Storage.BodyStorage.MinCompressionBytes"], Is.EqualTo("Default"));
            Assert.That(data["Storage.FreeSpaceThresholdPercent"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public async Task Should_report_configured_values_in_the_unit_their_keys_name()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_DATABASE_COMMANDTIMEOUT", "60");
        Environment.SetEnvironmentVariable("SERVICECONTROL_QUERYTIMEOUTINSECONDS", "120");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MESSAGEBODY_FILESYSTEM_DATASPACEREMAININGTHRESHOLD", "15");

        var data = await GetData(new TestPersisterSettings
        {
            ConnectionString = "Host=localhost",
            BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "/var/bodies", DataSpaceRemainingThreshold = 15 },
            CommandTimeout = 60,
            QueryTimeout = TimeSpan.FromSeconds(120)
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Storage.CommandTimeoutSeconds"], Is.EqualTo("60"));
            Assert.That(data["Storage.QueryTimeoutSeconds"], Is.EqualTo("120"));
            Assert.That(data["Storage.FreeSpaceThresholdPercent"], Is.EqualTo("15"));
            Assert.That(data["Storage.SubscriptionCacheSeconds"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public async Task Should_report_a_custom_schema_without_naming_it()
    {
        var data = await GetData(new TestPersisterSettings { ConnectionString = "Host=localhost", BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "/var/bodies" }, Schema = "contoso" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Storage.Schema"], Is.EqualTo("Custom"));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso"));
        }
    }

    [Test]
    public async Task Should_report_the_free_space_threshold_as_not_applicable_for_cloud_body_storage()
    {
        var data = await GetData(new TestPersisterSettings { ConnectionString = "Host=localhost", BodyStorage = new S3BodyStorageSettings { BucketName = "bodies" } });

        Assert.That(data["Storage.FreeSpaceThresholdPercent"], Is.EqualTo("NotApplicable"));
    }

    static async Task<Dictionary<string, string>> GetData(EFPersisterSettings settings)
    {
        var data = new Dictionary<string, string>();

        foreach (var datum in new EFEnvironmentDataProvider(settings, new StubHostingProbe(), new StubFootprintProbe(), scopeFactory: null).GetData())
        {
            try
            {
                data[datum.Key] = await datum.ReadValue(CancellationToken.None);
            }
            catch (Exception)
            {
                data[datum.Key] = Particular.LicensingComponent.Contracts.EnvironmentDatum.ReadFailed;
            }
        }

        return data;
    }

    static readonly string[] Variables =
    [
        "SERVICECONTROL_DATABASE_COMMANDTIMEOUT",
        "SERVICECONTROL_QUERYTIMEOUTINSECONDS",
        "SERVICECONTROL_MESSAGEBODY_FILESYSTEM_DATASPACEREMAININGTHRESHOLD"
    ];

    sealed class TestPersisterSettings : EFPersisterSettings;

    sealed class StubHostingProbe : IDatabaseHostingProbe
    {
        public string StorageName => "PostgreSQL";

        public Task<DatabaseHosting> Probe(CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseHosting.Unclassified);
    }

    sealed class StubFootprintProbe : IStorageFootprintProbe
    {
        public Task<StorageFootprint> Probe(CancellationToken cancellationToken = default) => Task.FromResult<StorageFootprint>(null);
    }
}
