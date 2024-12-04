namespace Particular.LicensingComponent.UnitTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using Contracts;
using Infrastructure;
using NUnit.Framework;

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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("False"), $"AuditEnabled should be False");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("True"), $"AuditEnabled should be True");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("False"), $"MonitoringEnabled should be False");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("True"), $"MonitoringEnabled should be True");
        });
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
        var report = await ThroughputCollector.GenerateThroughputReport(spVersion, null, default);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.ServicePulseVersion.ToString()), Is.True, $"ServicePulseVersion missing from Environment data");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.ServicePulseVersion.ToString()], Is.EqualTo(spVersion), $"ServicePulseVersion should be {spVersion}");
        });
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
        await DataStore.SaveBrokerMetadata(new BrokerMetadata(expectedScopeType, new Dictionary<string, string> { [EnvironmentDataType.BrokerVersion.ToString()] = expectedBrokerVersion }), default);

        var expectedAuditVersionSummary = new Dictionary<string, int> { ["4.3.6"] = 2 };
        var expectedAuditTransportSummary = new Dictionary<string, int> { ["AzureServiceBus"] = 2 };
        await DataStore.SaveAuditServiceMetadata(new AuditServiceMetadata(expectedAuditVersionSummary, expectedAuditTransportSummary), default);

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        Assert.That(report, Is.Not.Null);

        Assert.That(report.ReportData.EnvironmentInformation, Is.Not.Null, $"Environment information missing from the report");
        Assert.Multiple(() =>
        {
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData.ContainsKey(EnvironmentDataType.BrokerVersion.ToString()), Is.True, $"Missing EnvironmentData.Version from report");
            Assert.That(report.ReportData.EnvironmentInformation.EnvironmentData[EnvironmentDataType.BrokerVersion.ToString()], Is.EqualTo(expectedBrokerVersion), $"Incorrect EnvironmentData.Version on report");
            Assert.That(report.ReportData.EnvironmentInformation.AuditServicesData.Versions, Is.EquivalentTo(expectedAuditVersionSummary), $"Invalid AuditInstance version summary on report");
            Assert.That(report.ReportData.EnvironmentInformation.AuditServicesData.Transports, Is.EquivalentTo(expectedAuditTransportSummary), $"Invalid AuditInstance transport summary on report");
        });

        Assert.That(report.ReportData.ScopeType, Is.Not.Null, $"Missing ScopeType from report");
        Assert.That(report.ReportData.ScopeType, Is.EqualTo(expectedScopeType), $"Invalid ScopeType on report");
    }
}