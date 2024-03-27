namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;

using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_EnvironmentData_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    [Test]
    public async Task Should_set_audit_flag_to_false_when_no_audit_data()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentData.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentData.AuditEnabled.ToString()], Is.EqualTo("False"), $"AuditEnabled should be False");
    }

    [Test]
    public async Task Should_set_audit_flag_to_true_when_audit_data_exists()
    {
        EndpointsWithThroughputFromBrokerAndMonitoringAndAudit.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentData.AuditEnabled.ToString()), Is.True, $"AuditEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentData.AuditEnabled.ToString()], Is.EqualTo("True"), $"AuditEnabled should be True");
    }

    [Test]
    public async Task Should_set_monitoring_flag_to_false_when_no_audit_data()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentData.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentData.MonitoringEnabled.ToString()], Is.EqualTo("False"), $"MonitoringEnabled should be False");
    }

    [Test]
    public async Task Should_set_monitoring_flag_to_true_when_audit_data_exists()
    {
        EndpointsWithThroughputFromBrokerAndMonitoringAndAudit.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Environment data missing from the report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentData.MonitoringEnabled.ToString()), Is.True, $"MonitoringEnabled missing from Environment data");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentData.MonitoringEnabled.ToString()], Is.EqualTo("True"), $"MonitoringEnabled should be True");
    }

    [Test]
    public async Task Should_set_sp_version_if_provided()
    {
        EndpointsWithThroughputFromBrokerAndMonitoringAndAudit.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var spVersion = "5.1";
        var report = await ThroughputCollector.GenerateThroughputReport(null, null, spVersion);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.ServicePulseVersion, Is.Not.Null, $"ServicePulseVersion missing from the report");
        Assert.That(report.ReportData.ServicePulseVersion, Is.EqualTo(spVersion), $"ServicePulseVersion should be {spVersion}");
    }

    [Test]
    public async Task Should_include_broker_data_in_report()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var version = "1.2";
        var scopeType = "testingScope";
        var brokerData = new Dictionary<string, string>
        {
            { EnvironmentData.Version.ToString(), version }
        };
        await DataStore.SaveBrokerData(broker, scopeType, brokerData);

        var report = await ThroughputCollector.GenerateThroughputReport(null, null, null);

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.EnvironmentData, Is.Not.Null, $"Missing EnvironmentData from report");
        Assert.That(report.ReportData.ScopeType, Is.Not.Null, $"Missing ScopeType from report");
        Assert.That(report.ReportData.ScopeType, Is.EqualTo(scopeType), $"Invalid ScopeType on report");
        Assert.That(report.ReportData.EnvironmentData.ContainsKey(EnvironmentData.Version.ToString()), Is.True, $"Missing EnvironmentData.Version from report");
        Assert.That(report.ReportData.EnvironmentData[EnvironmentData.Version.ToString()], Is.EqualTo(version), $"Incorrect EnvironmentData.Version on report");
    }

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerOnly =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoringAndAudit =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Audit) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 61 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 64 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 57 }] },
        new Endpoint("Endpoint3", ThroughputSource.Monitoring) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 45 }] },
        new Endpoint("Endpoint3", ThroughputSource.Audit) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 42 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 47 }] }
    ];
}