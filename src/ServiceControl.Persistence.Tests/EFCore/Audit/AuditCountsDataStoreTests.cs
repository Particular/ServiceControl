namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

class AuditCountsDataStoreTests : AuditIngestionTestBase
{
    IAuditCountsDataStore Counts => ServiceProvider.GetRequiredService<IAuditCountsDataStore>();

    [Test]
    public async Task Counts_the_endpoints_non_system_messages_per_day_oldest_first()
    {
        var today = Now.Date;

        await IngestAudit(
            Sales(today.AddDays(-1).AddHours(3)),
            Sales(today.AddDays(-1).AddHours(4)),
            Sales(today.AddDays(-2).AddHours(1)),
            Sales(today.AddDays(-1).AddHours(5), isSystemMessage: true),
            new IngestedAudit { EndpointName = "Billing", ReceivingEndpoint = new() { Name = "Billing", Host = "h", HostId = Guid.NewGuid() }, ProcessingEnded = today.AddDays(-1) });

        var result = await Counts.QueryAuditCounts("Sales");

        Assert.That(result.Results.Select(count => (count.UtcDate, count.Count)), Is.EqualTo(new[] { (today.AddDays(-2), 1L), (today.AddDays(-1), 2L) }));
    }

    [Test]
    public async Task Ignores_messages_older_than_thirty_days()
    {
        var today = Now.Date;

        await IngestAudit(Sales(today.AddDays(-31)), Sales(today.AddDays(-29)));

        var result = await Counts.QueryAuditCounts("Sales");

        Assert.That(result.Results.Select(count => count.UtcDate), Is.EqualTo(new[] { today.AddDays(-29) }));
    }

    [Test]
    public async Task Reports_a_zero_for_today_when_the_endpoint_only_ever_sent()
    {
        await IngestAudit(new IngestedAudit { SendingEndpoint = new() { Name = "Ordering", Host = "h", HostId = Guid.NewGuid() } });

        var result = await Counts.QueryAuditCounts("Ordering");

        Assert.That(result.Results.Select(count => (count.UtcDate, count.Count)), Is.EqualTo(new[] { (Now.Date, 0L) }));
    }

    [Test]
    public async Task Reports_nothing_for_an_unknown_endpoint()
    {
        await IngestAudit(new IngestedAudit());

        var result = await Counts.QueryAuditCounts("Nobody");

        Assert.That(result.Results, Is.Empty);
    }

    IngestedAudit Sales(DateTime processedAt, bool isSystemMessage = false) => new()
    {
        EndpointName = "Sales",
        ReceivingEndpoint = new() { Name = "Sales", Host = "h", HostId = Guid.NewGuid() },
        TimeSent = processedAt.AddSeconds(-2),
        ProcessingStarted = processedAt.AddSeconds(-1),
        ProcessingEnded = processedAt,
        IsSystemMessage = isSystemMessage
    };
}
