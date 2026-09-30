namespace Particular.LicensingComponent.UnitTests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Contracts;
using Infrastructure;

[TestFixture]
class ThroughputCollector_Report_EnvironmentInformation_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_set_audit_flag_to_false_when_no_audit_data()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("False"), $"AuditEnabled should be False");
        }
    }

    [Test]
    public async Task Should_set_audit_flag_to_true_when_audit_data_exists()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Monitoring, days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Audit, days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Monitoring, days: 2)
                .WithThroughput(ThroughputSource.Audit, days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("True"), $"AuditEnabled should be True");
        }
    }

    [Test]
    public async Task Should_set_monitoring_flag_to_false_when_no_monitoring_data()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("False"), $"MonitoringEnabled should be False");
        }
    }

    [Test]
    public async Task Should_set_monitoring_flag_to_true_when_monitoring_data_exists()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Monitoring, days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Audit, days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Monitoring, days: 2)
                .WithThroughput(ThroughputSource.Audit, days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("True"), $"MonitoringEnabled should be True");
        }
    }

    [Test]
    public async Task Should_set_sp_version_if_provided()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Monitoring, days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Audit, days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, days: 2)
                .WithThroughput(ThroughputSource.Monitoring, days: 2)
                .WithThroughput(ThroughputSource.Audit, days: 2)
            .Build();

        // Act
        var spVersion = "5.1";
        var report = await ThroughputCollector.GenerateThroughputReport(spVersion, null);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.ServicePulseVersion.ToString()), Is.True, $"ServicePulseVersion missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServicePulseVersion.ToString()], Is.EqualTo(spVersion), $"ServicePulseVersion should be {spVersion}");
        }
    }

    [Test]
    public async Task Should_include_environment_data_in_report()
    {
        // Arrange
        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker]).WithThroughput(days: 2)
            .Build();

        var expectedBrokerVersion = "1.2";
        var expectedScopeType = "testingScope";
        await DataStore.SaveBrokerMetadata(new BrokerMetadata(expectedScopeType, new Dictionary<string, string> { [EnvironmentDataType.BrokerVersion.ToString()] = expectedBrokerVersion }));

        var expectedAuditVersionSummary = new Dictionary<string, int> { ["4.3.6"] = 2 };
        var expectedAuditTransportSummary = new Dictionary<string, int> { ["AzureServiceBus"] = 2 };
        await DataStore.SaveAuditServiceMetadata(new AuditServiceMetadata(expectedAuditVersionSummary, expectedAuditTransportSummary));

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        // Assert
        Assert.That(report, Is.Not.Null);

        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.BrokerVersion.ToString()), Is.True, $"Missing EnvironmentData.Version from report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.BrokerVersion.ToString()], Is.EqualTo(expectedBrokerVersion), $"Incorrect EnvironmentData.Version on report");
            Assert.That(report.ReportData.EnvironmentInformation.AuditServicesData.Versions, Is.EquivalentTo(expectedAuditVersionSummary), $"Invalid AuditInstance version summary on report");
            Assert.That(report.ReportData.EnvironmentInformation.AuditServicesData.Transports, Is.EquivalentTo(expectedAuditTransportSummary), $"Invalid AuditInstance transport summary on report");
        }

        Assert.That(report.ReportData.ScopeType, Is.Not.Null, $"Missing ScopeType from report");
        Assert.That(report.ReportData.ScopeType, Is.EqualTo(expectedScopeType), $"Invalid ScopeType on report");
    }

    [Test]
    public async Task Should_include_audit_instance_counts_in_environment_data()
    {
        await DataStore.SaveAuditServiceMetadata(new AuditServiceMetadata([], []) { ConfiguredInstances = 50, LiveInstances = 2 });

        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        var environmentData = report.ReportData.EnvironmentInformation.EnvironmentData;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(environmentData, Does.ContainKey("Audit.ConfiguredInstances").WithValue("50"));
            Assert.That(environmentData, Does.ContainKey("Audit.LiveInstances").WithValue("2"));
        }
    }

    [Test]
    public async Task Should_leave_audit_instance_counts_out_of_environment_data_when_none_are_stored()
    {
        await DataStore.SaveAuditServiceMetadata(new AuditServiceMetadata(
            new Dictionary<string, int> { ["6.2.0"] = 1 },
            new Dictionary<string, int> { ["RabbitMQ.QuorumConventionalRouting"] = 1 }));

        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        var environmentData = report.ReportData.EnvironmentInformation.EnvironmentData;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(environmentData, Does.Not.ContainKey("Audit.ConfiguredInstances"));
            Assert.That(environmentData, Does.Not.ContainKey("Audit.LiveInstances"));
        }
    }

    [Test]
    public async Task Should_report_audit_environment_data_aggregated_across_instances()
    {
        await DataStore.SaveAuditEnvironmentMetadata(new AuditEnvironmentMetadata(
        [
            new Dictionary<string, string>
            {
                ["Storage.Type"] = "RavenDB",
                ["Storage.ServerVersion"] = "6.2.1",
                ["Host.ProcessorCount"] = "4",
                ["Host.AvailableMemoryGB"] = "16",
                ["Host.OSPlatform"] = "Linux"
            },
            new Dictionary<string, string>
            {
                ["Storage.Type"] = "RavenDB",
                ["Storage.ServerVersion"] = "5.4.200",
                ["Host.ProcessorCount"] = "8",
                ["Host.AvailableMemoryGB"] = "Unknown"
            }
        ]));

        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        var environmentData = report.ReportData.EnvironmentInformation.EnvironmentData;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(environmentData, Does.ContainKey("Audit.Storage.Type").WithValue("RavenDB"));
            Assert.That(environmentData, Does.ContainKey("Audit.Storage.ServerVersion").WithValue("Mixed"));
            Assert.That(environmentData, Does.ContainKey("Audit.Host.ProcessorCount").WithValue("8"));
            Assert.That(environmentData, Does.ContainKey("Audit.Host.AvailableMemoryGB").WithValue("Mixed"));
            Assert.That(environmentData, Does.ContainKey("Audit.Host.OSPlatform").WithValue("Linux"));
        }
    }

    [Test]
    public async Task Should_leave_audit_environment_data_out_when_it_was_never_collected()
    {
        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.Keys, Has.None.StartsWith("Audit.Storage."));
    }

    [Test]
    public async Task Should_leave_audit_environment_data_out_when_no_instance_responded()
    {
        await DataStore.SaveAuditEnvironmentMetadata(new AuditEnvironmentMetadata([]));

        var report = await ThroughputCollector.GenerateThroughputReport("", null);

        Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.Keys, Has.None.StartsWith("Audit.Storage."));
    }
}