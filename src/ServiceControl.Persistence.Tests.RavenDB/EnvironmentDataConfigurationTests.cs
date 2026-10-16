namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.RavenDB;

[TestFixture]
[NonParallelizable]
class RavenEnvironmentDataProviderConfigurationTests
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
        var data = await GetData(new RavenPersisterSettings());

        using (Assert.EnterMultipleScope())
        {
            // RavenDB 7 replaced 'Logs.Mode' (None/Operations/Information) with 'Logs.MinLevel', which
            // takes a Sparrow.Logging.LogLevel name, so the quiet default now reports as "Warn".
            Assert.That(data["Storage.LogLevel"], Is.EqualTo("Warn"));
            Assert.That(data["Storage.QueryTimeoutSeconds"], Is.EqualTo("Default"));
            Assert.That(data["Storage.FreeSpaceThresholdPercent"], Is.EqualTo("Default"));
            Assert.That(data["Storage.MinimumFreeSpaceForIngestionPercent"], Is.EqualTo("Default"));
            Assert.That(data["Storage.ExpirationIntervalSeconds"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public async Task Should_report_configured_tuning_values()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_DATASPACEREMAININGTHRESHOLD", "30");
        Environment.SetEnvironmentVariable("SERVICECONTROL_EXPIRATIONPROCESSTIMERINSECONDS", "300");

        var data = await GetData(new RavenPersisterSettings { DataSpaceRemainingThreshold = 30, ExpirationProcessTimerInSeconds = 300 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Storage.FreeSpaceThresholdPercent"], Is.EqualTo("30"));
            Assert.That(data["Storage.ExpirationIntervalSeconds"], Is.EqualTo("300"));
            Assert.That(data["Storage.MinimumFreeSpaceForIngestionPercent"], Is.EqualTo("Default"));
        }
    }

    static async Task<Dictionary<string, string>> GetData(RavenPersisterSettings settings)
    {
        var data = new Dictionary<string, string>();

        foreach (var datum in new RavenEnvironmentDataProvider(settings, documentStoreProvider: null).GetData())
        {
            if (ConfigurationKeys.Contains(datum.Key))
            {
                data[datum.Key] = await datum.ReadValue(CancellationToken.None);
            }
        }

        return data;
    }

    static readonly HashSet<string> ConfigurationKeys =
    [
        "Storage.LogLevel",
        "Storage.QueryTimeoutSeconds",
        "Storage.FreeSpaceThresholdPercent",
        "Storage.MinimumFreeSpaceForIngestionPercent",
        "Storage.ExpirationIntervalSeconds"
    ];

    static readonly string[] Variables =
    [
        "SERVICECONTROL_DATASPACEREMAININGTHRESHOLD",
        "SERVICECONTROL_EXPIRATIONPROCESSTIMERINSECONDS"
    ];
}
