namespace Particular.ThroughputCollector.UnitTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class ThroughputCollector_Report_Dates_Tests : ThroughputCollectorTestFixture
{
    public override Task Setup()
    {
        SetExtraDependencies = d => { };

        return base.Setup();
    }

    [Test]
    public async Task Should_return_correct_dates_for_report_when_multiple_sources_with_different_dates()
    {
        // Arrange
        var maxDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var minDate = maxDate.AddDays(-4);

        await DataStore.CreateBuilder()
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring])
                .WithThroughput(ThroughputSource.Broker, startDate: maxDate, days: 1)
                .WithThroughput(ThroughputSource.Broker, startDate: maxDate.AddDays(-3), days: 1)
                .WithThroughput(ThroughputSource.Monitoring, startDate: maxDate.AddDays(-1), days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, startDate: maxDate, days: 1)
                .WithThroughput(ThroughputSource.Broker, startDate: maxDate.AddDays(-2), days: 1)
                .WithThroughput(ThroughputSource.Audit, startDate: maxDate.AddDays(-1), days: 2)
            .AddEndpoint(sources: [ThroughputSource.Broker, ThroughputSource.Monitoring, ThroughputSource.Audit])
                .WithThroughput(ThroughputSource.Broker, startDate: maxDate.AddDays(-1), days: 2)
                .WithThroughput(ThroughputSource.Monitoring, startDate: maxDate, days: 1)
                .WithThroughput(ThroughputSource.Monitoring, startDate: minDate, days: 1)
                .WithThroughput(ThroughputSource.Audit, startDate: maxDate.AddDays(-1), days: 2)
            .Build();

        // Act
        var report = await ThroughputCollector.GenerateThroughputReport("", null, default);

        // Assert
        var minDateInReport = new DateTimeOffset(minDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var reportEndDate = new DateTimeOffset(maxDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        Assert.That(report, Is.Not.Null);
        Assert.That(report.ReportData.Queues.Count, Is.EqualTo(3));

        Assert.That(report.ReportData.StartTime, Is.EqualTo(minDateInReport), $"Incorrect StartTime for report");
        Assert.That(report.ReportData.EndTime, Is.EqualTo(reportEndDate), $"Incorrect StartTime for report");
        Assert.That(report.ReportData.ReportDuration, Is.EqualTo(reportEndDate - minDateInReport), $"Incorrect ReportDuration for report");
    }
}