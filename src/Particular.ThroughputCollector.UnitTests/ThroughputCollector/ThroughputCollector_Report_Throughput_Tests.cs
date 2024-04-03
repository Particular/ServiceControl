namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Throughput_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;
    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }


    [Test]
    public async Task Should_not_include_daily_throughput_for_non_nsb_endpoints()
    {
        EndpointsWithThroughputFromBrokerAndOneNonNsbEndpoint.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3")?.DailyThroughputFromBroker?.Length, Is.EqualTo(0), $"Incorrect number of endpoints in throughput report");
    }

    [Test]
    public async Task Should_include_daily_throughput_for_endpoints_with_userIndicator_other_than_non_nsb_endpoints()
    {
        EndpointsWithThroughputFromBrokerWithUserIndicatorsOtherThanNonNsbEndpoint.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(4), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Incorrect number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint4")?.DailyThroughputFromBroker?.Length, Is.EqualTo(2), $"Incorrect number of endpoints in throughput report");
    }

    [Test]
    public async Task Should_return_correct_number_of_endpoints_in_report_when_only_one_source_of_throughput()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput report");
    }

    [Test]
    public async Task Should_return_correct_number_of_endpoints_in_report_when_multiple_sources_of_throughput()
    {
        EndpointsWithThroughputFromBrokerAndMonitoring.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3), $"Incorrect number of endpoints in throughput report");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_data_only_from_one_source()
    {
        EndpointsWithThroughputFromBrokerOnly.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1").Throughput, Is.EqualTo(55), $"Incorrect Throughput recorded for Endpoint1");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint2");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3").Throughput, Is.EqualTo(75), $"Incorrect Throughput recorded for Endpoint3");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(195), $"Incorrect TotalThroughput recorded for Endpoint1");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Incorrect TotalQueues recorded for Endpoint1");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_multiple_sources()
    {
        EndpointsWithThroughputFromBrokerAndMonitoringAndAudit.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint1").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint1");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint2").Throughput, Is.EqualTo(65), $"Incorrect Throughput recorded for Endpoint2");
        Assert.That(report.ReportData.Queues.FirstOrDefault(w => w.QueueName == "Endpoint3").Throughput, Is.EqualTo(57), $"Incorrect Throughput recorded for Endpoint3");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(187), $"Incorrect TotalThroughput recorded for Endpoint1");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(3), $"Incorrect TotalQueues recorded for Endpoint1");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_endpoint_has_no_throughput()
    {
        EndpointsWithNoThroughput.ForEach(e => DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput));

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1), "Invalid number of endpoints in throughput report");
        Assert.That(report.ReportData.Queues[0].Throughput, Is.EqualTo(0), $"Incorrect Throughput recorded for {report.ReportData.Queues[0].QueueName}");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(0), $"Incorrect TotalThroughput recorded for Endpoint1");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Incorrect TotalQueues recorded for Endpoint1");
    }

    [Test]
    public async Task Should_return_correct_throughput_in_report_when_data_from_multiple_sources_and_name_is_different()
    {
        EndpointsWithDifferentNamesButSameSanitizedNames.ForEach(async e =>
        {
            await DataStore.SaveEndpoint(e);
            await DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput);
        });

        var report = await ThroughputCollector.GenerateThroughputReport([], "");

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(1));

        //we want to see the name for the endpoint if one exists, not the broker sanitized name
        Assert.That(report.ReportData.Queues[0].QueueName, Is.EqualTo("Endpoint1_"), $"Incorrect Name for Endpoint1");

        //even though the names are different, we should have matched on the sanitized name and hence displayed max throughput from the 2 endpoints
        Assert.That(report.ReportData.Queues[0].Throughput, Is.EqualTo(75), $"Incorrect Throughput recorded for Endpoint1");

        Assert.That(report.ReportData.TotalThroughput, Is.EqualTo(75), $"Incorrect TotalThroughput recorded for Endpoint1");
        Assert.That(report.ReportData.TotalQueues, Is.EqualTo(1), $"Incorrect TotalQueues recorded for Endpoint1");
    }

    readonly List<Endpoint> EndpointsWithNoThroughput =
    [
        new Endpoint("Endpoint1", ThroughputSource.Audit) { SanitizedName = "Endpoint1" },
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerAndOneNonNsbEndpoint =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", UserIndicator = UserIndicator.NServicebusEndpoint.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", UserIndicator = UserIndicator.NServicebusEndpoint.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", UserIndicator = UserIndicator.NotNServicebusEndpoint.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerWithUserIndicatorsOtherThanNonNsbEndpoint =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", UserIndicator = UserIndicator.NServicebusEndpoint.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", UserIndicator = UserIndicator.NServicebusEndpointNoLongerInUse.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", UserIndicator = UserIndicator.NServicebusEndpointSendOnly.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] },
        new Endpoint("Endpoint4", ThroughputSource.Broker) { SanitizedName = "Endpoint4", UserIndicator = UserIndicator.NServicebusEndpointScaledOut.ToString(), DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerOnly =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 75 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 50 }] }
    ];

    readonly List<Endpoint> EndpointsWithThroughputFromBrokerAndMonitoring =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint2", ThroughputSource.Broker) { SanitizedName = "Endpoint2", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
        new Endpoint("Endpoint3", ThroughputSource.Broker) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 55 }] },
        new Endpoint("Endpoint1", ThroughputSource.Monitoring) { SanitizedName = "Endpoint3", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 40 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 45 }] }
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

    readonly List<Endpoint> EndpointsWithDifferentNamesButSameSanitizedNames =
    [
        new Endpoint("Endpoint1", ThroughputSource.Broker) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 50 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 75 }] },
        new Endpoint("Endpoint1_", ThroughputSource.Audit) { SanitizedName = "Endpoint1", DailyThroughput = [new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1), TotalThroughput = 60 }, new EndpointDailyThroughput { DateUTC = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2), TotalThroughput = 65 }] },
    ];
}