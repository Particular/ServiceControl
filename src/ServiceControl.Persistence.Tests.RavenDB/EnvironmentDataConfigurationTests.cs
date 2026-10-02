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
    public async Task Should_report_an_embedded_server_with_its_defaults()
    {
        var data = await GetData(new RavenPersisterSettings());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Storage.Auth"], Is.EqualTo("NotApplicable"));
            Assert.That(data["Storage.LogLevel"], Is.EqualTo("Operations"));
            Assert.That(data["Storage.QueryTimeoutSeconds"], Is.EqualTo("Default"));
            Assert.That(data["Storage.FreeSpaceThresholdPercent"], Is.EqualTo("Default"));
            Assert.That(data["Storage.MinimumFreeSpaceForIngestionPercent"], Is.EqualTo("Default"));
            Assert.That(data["Storage.ExpirationIntervalSeconds"], Is.EqualTo("Default"));
        }
    }

    [TestCase(null, "None")]
    [TestCase("/certs/contoso.pfx", "ClientCertificate")]
    public async Task Should_report_how_an_external_server_authenticates_without_naming_it(string clientCertificatePath, string expected)
    {
        var data = await GetData(new RavenPersisterSettings { ConnectionString = "https://raven.contoso.local:8080", ClientCertificatePath = clientCertificatePath });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Storage.Auth"], Is.EqualTo(expected));
            Assert.That(string.Join("|", data.Values), Does.Not.Contain("contoso"));
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
        "Storage.Auth",
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
