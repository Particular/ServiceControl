namespace Particular.ThroughputCollector.UnitTests;

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;
using ServiceControl.Api;

[TestFixture]
class ThroughputCollector_Report_EnvironmentData_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d =>
        {
            d.AddSingleton<IConfigurationApi, FakeConfigurationApi>();
            d.AddSingleton<IEndpointsApi, FakeEndpointApi>();
            d.AddSingleton<IAuditCountApi, FakeAuditCountApi>();
        };

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
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("False"), $"AuditEnabled should be False");
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
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentDataType.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentDataType.AuditEnabled.ToString()], Is.EqualTo("True"), $"AuditEnabled should be True");
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
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("False"), $"MonitoringEnabled should be False");
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
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentDataType.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentDataType.MonitoringEnabled.ToString()], Is.EqualTo("True"), $"MonitoringEnabled should be True");
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
        var report = await ThroughputCollector.GenerateThroughputReport([], spVersion);

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.ServicePulseVersion, Is.Not.Null, $"ServicePulseVersion missing from the report");
        Assert.That(report.ReportData.ServicePulseVersion, Is.EqualTo(spVersion), $"ServicePulseVersion should be {spVersion}");
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

        var version = "1.2";
        var scopeType = "testingScope";
        var brokerData = new Dictionary<string, string>
        {
            { EnvironmentDataType.Version.ToString(), version }
        };
        await DataStore.SaveEnvironmentData(scopeType, brokerData);

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        // Assert
        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Missing EnvironmentData from report");
        Assert.That(report.ReportData.ScopeType, Is.Not.Null, $"Missing ScopeType from report");
        Assert.That(report.ReportData.ScopeType, Is.EqualTo(scopeType), $"Invalid ScopeType on report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentDataType.Version.ToString()), Is.True, $"Missing EnvironmentData.Version from report");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentDataType.Version.ToString()], Is.EqualTo(version), $"Incorrect EnvironmentData.Version on report");
    }
}