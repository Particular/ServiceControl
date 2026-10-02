namespace ServiceControl.UnitTests.Licensing;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ServiceControl;
using ServiceBus.Management.Infrastructure.Settings;

[TestFixture]
[NonParallelizable]
class ErrorInstanceTuningEnvironmentDataProviderTests
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
    public async Task Should_report_default_for_every_setting_left_unset()
    {
        var data = await GetData();

        Assert.That(data.Values, Has.All.EqualTo("Default"));
    }

    [Test]
    public async Task Should_report_the_configured_value_in_the_unit_its_key_names()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_HEARTBEATGRACEPERIOD", "00:01:30");
        Environment.SetEnvironmentVariable("SERVICECONTROL_ERRORINGESTIONBATCHTIMEOUT", "00:00:00.250");
        Environment.SetEnvironmentVariable("SERVICECONTROL_MAXIMUMCONCURRENCYLEVEL", "64");
        Environment.SetEnvironmentVariable("SERVICECONTROL_RETRYHISTORYDEPTH", "20");
        Environment.SetEnvironmentVariable("SERVICECONTROL_SHUTDOWNTIMEOUT", "00:02:00");

        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Heartbeats.GracePeriodSeconds"], Is.EqualTo("90"));
            Assert.That(data["Ingestion.Error.BatchTimeoutMs"], Is.EqualTo("250"));
            Assert.That(data["Ingestion.Error.MaxConcurrency"], Is.EqualTo("64"));
            Assert.That(data["Recoverability.RetryHistoryDepth"], Is.EqualTo("20"));
            Assert.That(data["Host.ShutdownTimeoutSeconds"], Is.EqualTo("120"));
            Assert.That(data["Ingestion.Error.BatchSize"], Is.EqualTo("Default"));
        }
    }

    [Test]
    public async Task Should_report_a_value_set_to_the_default_as_configured()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_RETRYHISTORYDEPTH", "10");

        var data = await GetData();

        Assert.That(data["Recoverability.RetryHistoryDepth"], Is.EqualTo("10"));
    }

    static async Task<Dictionary<string, string>> GetData()
    {
        var data = new Dictionary<string, string>();

        foreach (var datum in new ErrorInstanceTuningEnvironmentDataProvider(new Settings()).GetData())
        {
            data[datum.Key] = await datum.ReadValue(CancellationToken.None);
        }

        return data;
    }

    static readonly string[] Variables =
    [
        "SERVICECONTROL_HEARTBEATGRACEPERIOD",
        "SERVICECONTROL_ERRORINGESTIONBATCHTIMEOUT",
        "SERVICECONTROL_MAXIMUMCONCURRENCYLEVEL",
        "SERVICECONTROL_RETRYHISTORYDEPTH",
        "SERVICECONTROL_SHUTDOWNTIMEOUT"
    ];
}
