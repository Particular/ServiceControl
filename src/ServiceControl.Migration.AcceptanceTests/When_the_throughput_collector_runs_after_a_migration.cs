namespace ServiceControl.Migration.AcceptanceTests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Particular.LicensingComponent.Contracts;

[TestFixture]
[NonParallelizable]
class When_the_throughput_collector_runs_after_a_migration : MigrationAcceptanceTest
{
    [Test]
    public async Task Copied_days_are_not_collected_a_second_time_and_added_to()
    {
        var time = new FakeTimeProvider();
        var yesterday = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime).AddDays(-1);

        await SeedThroughputSource(new Endpoint("Sales.Orders", ThroughputSource.Broker) { SanitizedName = "sales.orders" });
        await SeedThroughputTimeSeries("Sales.Orders/Broker", yesterday, 100);
        BrokerReports(queue: "sales.orders", day: yesterday, count: 70);

        await RunRequiredCopy(customize: services =>
        {
            services.AddSingleton<TimeProvider>(time);
            CollectFromTheBrokerImmediately(services);
        });

        await WaitForCollectorPass();

        var total = await QueryTarget(dbContext => dbContext.LicensingEndpointThroughput
            .Where(row => row.NormalizedName == "sales.orders" && row.ThroughputSource == ThroughputSource.Broker && row.DateUtc == yesterday)
            .Select(row => row.MessageCount)
            .SingleAsync());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BrokerRequests, Is.EqualTo(new[] { ("sales.orders", yesterday.AddDays(1)) }),
                "the collector asks only for days after the last copied one, so it never tried to collect the copied day again and a failed write cannot hide that it did");
            Assert.That(total, Is.EqualTo(100),
                "100 is the copied count; 70 means the copy wrote nothing and the collector collected the day itself; 170 means the collector re-collected a copied day and the additive write path added the two, so the licensing report overstates usage");
        }
    }
}
