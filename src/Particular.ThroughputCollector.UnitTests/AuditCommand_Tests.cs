namespace Particular.ThroughputCollector.UnitTests;

using System.Threading.Tasks;
using NUnit.Framework;
using Particular.ThroughputCollector.Contracts;
using Particular.ThroughputCollector.UnitTests.Infrastructure;

[TestFixture]
class AuditCommand_Tests : ThroughputCollectorTestFixture
{
    readonly Broker broker = Broker.AzureServiceBus;

    public override Task Setup()
    {
        SetThroughputSettings = s => s.Broker = broker;

        return base.Setup();
    }

    //[Test]
    //public async Task Should_return_ReportCanBeGenerated_false_when_no_throughput_for_last_30_days()
    //{
    //    EndpointsWithNoThroughputInLast30Days.ForEach(e => DataStore.RecordEndpointThroughput(e.Id, e.DailyThroughput));

    //    var reportGenerationState = await ThroughputCollector.GetReportGenerationState();

    //    Assert.That(reportGenerationState.ReportCanBeGenerated, Is.False);
    //}
}