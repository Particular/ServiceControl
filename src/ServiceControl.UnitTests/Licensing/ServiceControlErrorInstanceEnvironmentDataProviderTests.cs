namespace ServiceControl.UnitTests.Licensing;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Particular.ServiceControl;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Notifications;
using ServiceControl.Persistence;

[TestFixture]
[NonParallelizable]
class ServiceControlErrorInstanceEnvironmentDataProviderTests
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
    public async Task Should_report_configuration_validation_disabled()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_VALIDATECONFIG", "false");

        var data = await GetData();

        Assert.That(data["Features.ConfigurationValidation"], Is.EqualTo("Disabled"));
    }

    [Test]
    public async Task Should_report_servicepulse_choices_as_not_applicable_when_it_is_not_integrated()
    {
        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["ServicePulse.MonitoringUrl"], Is.EqualTo("NotApplicable"));
            Assert.That(data["ServicePulse.DefaultRoute"], Is.EqualTo("NotApplicable"));
            Assert.That(data["ServicePulse.ShowPendingRetry"], Is.EqualTo("NotApplicable"));
        }
    }

    [Test]
    public async Task Should_report_integrated_servicepulse_choices_without_their_values()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE", "true");
        Environment.SetEnvironmentVariable("MONITORING_URL", "!");
        Environment.SetEnvironmentVariable("DEFAULT_ROUTE", "/contoso-failed-messages");
        Environment.SetEnvironmentVariable("SHOW_PENDING_RETRY", "true");

        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["ServicePulse.MonitoringUrl"], Is.EqualTo("Disabled"));
            Assert.That(data["ServicePulse.DefaultRoute"], Is.EqualTo("Custom"));
            Assert.That(data["ServicePulse.ShowPendingRetry"], Is.EqualTo("Enabled"));
            Assert.That(Reported(data), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    [Test]
    public async Task Should_report_a_custom_monitoring_url_for_integrated_servicepulse()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE", "true");
        Environment.SetEnvironmentVariable("MONITORING_URL", "http://monitoring.contoso.local:33633/");

        var data = await GetData();

        Assert.That(data["ServicePulse.MonitoringUrl"], Is.EqualTo("Custom"));
    }

    [Test]
    public async Task Should_report_feature_switches_from_configuration()
    {
        Environment.SetEnvironmentVariable("SERVICECONTROL_INGESTERRORMESSAGES", "false");
        Environment.SetEnvironmentVariable("SERVICECONTROL_VIRTUALDIRECTORY", "contoso");

        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Features.ErrorIngestion"], Is.EqualTo("Disabled"));
            Assert.That(data["Host.VirtualDirectory"], Is.EqualTo("Configured"));
            Assert.That(Reported(data), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    [Test]
    public async Task Should_report_otlp_metrics_export_without_the_endpoint()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://collector.contoso.local:4317" })
            .Build();

        var data = await GetData(configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Telemetry.OtlpMetrics"], Is.EqualTo("Enabled"));
            Assert.That(Reported(data), Does.Not.Contain("contoso").IgnoreCase);
        }
    }

    [Test]
    public async Task Should_report_logging_as_fixed_names()
    {
        var data = await GetData();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(data["Logging.Level"], Is.AnyOf("Trace", "Debug", "Information", "Warning", "Error", "Critical", "None"));
            Assert.That(data["Logging.Providers"].Split(','), Is.SubsetOf(new[] { "NLog", "Seq", "Otlp", "None" }));
            Assert.That(data["Telemetry.OtlpMetrics"], Is.EqualTo("Disabled"));
        }
    }

    static async Task<Dictionary<string, string>> GetData(IConfiguration configuration = null)
    {
        var provider = new ServiceControlErrorInstanceEnvironmentDataProvider(
            new Settings(),
            new StoredNotifications(),
            configuration ?? new ConfigurationBuilder().Build());

        var data = new Dictionary<string, string>();

        foreach (var datum in provider.GetData())
        {
            data[datum.Key] = await datum.ReadValue(CancellationToken.None);
        }

        return data;
    }

    static string Reported(Dictionary<string, string> data) => string.Join("|", data.Values);

    class StoredNotifications : INotificationsDataStore
    {
        public Task<NotificationsSettings> LoadSettings(CancellationToken cancellationToken = default) =>
            Task.FromResult(new NotificationsSettings());

        public Task SaveSettings(NotificationsSettings settings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    static readonly string[] Variables =
    [
        "SERVICECONTROL_VALIDATECONFIG",
        "SERVICECONTROL_ENABLEINTEGRATEDSERVICEPULSE",
        "MONITORING_URL",
        "DEFAULT_ROUTE",
        "SHOW_PENDING_RETRY",
        "SERVICECONTROL_INGESTERRORMESSAGES",
        "SERVICECONTROL_VIRTUALDIRECTORY"
    ];
}
