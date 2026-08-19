namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading.Tasks;
using Contracts.CustomChecks;
using NUnit.Framework;
using ServiceControl.Operations;
using ServiceControl.Persistence.Infrastructure;

[TestFixture]
class CustomCheckVersionTests : PersistenceTestBase
{
    static readonly DateTime ReportedAt = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Version_changes_when_a_check_starts_failing()
    {
        await Report("Disk space", hasFailed: false);

        var before = await CustomChecks.GetStats(new PagingInfo());

        await Report("Disk space", hasFailed: true);

        var after = await CustomChecks.GetStats(new PagingInfo());

        Assert.Multiple(() =>
        {
            Assert.That(after.Results, Has.Count.EqualTo(1), "still one check");
            Assert.That(after.Results[0].Status, Is.EqualTo(Status.Fail), "and the body now reports it failing");
            Assert.That(before.QueryStats.Version.HasValue, Is.True, "there was no version to move");
            Assert.That(after.QueryStats.Version.Matches(before.QueryStats.Version), Is.False,
                "the body changed, so the validator must too, or a revalidating client is shown a stale status");
        });
    }

    [Test]
    public async Task Version_changes_when_a_new_check_appears()
    {
        await Report("Disk space", hasFailed: false);

        var before = await CustomChecks.GetStats(new PagingInfo());

        await Report("Queue length", hasFailed: false);

        var after = await CustomChecks.GetStats(new PagingInfo());

        VersionAssert.Moved(before.QueryStats.Version, after.QueryStats.Version,
            "a check appeared, so a revalidating client must not be told its page is current");
    }

    [Test]
    public async Task Version_is_stable_while_nothing_changes()
    {
        await Report("Disk space", hasFailed: false);

        var first = await CustomChecks.GetStats(new PagingInfo());
        var second = await CustomChecks.GetStats(new PagingInfo());

        VersionAssert.Held(first.QueryStats.Version, second.QueryStats.Version,
            "nothing changed, so the validator has to stay put or conditional GET never pays off");
    }

    [Test]
    public async Task An_empty_store_still_reports_a_version()
    {
        var result = await CustomChecks.GetStats(new PagingInfo());

        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.QueryStats.Version.HasValue, Is.True,
                "an empty list is a representation like any other and has to be cacheable");
        });
    }

    async Task Report(string customCheckId, bool hasFailed)
    {
        await CustomChecks.UpdateCustomCheckStatus(new CustomCheckDetail
        {
            Category = "test-category",
            CustomCheckId = customCheckId,
            HasFailed = hasFailed,
            FailureReason = hasFailed ? "Testing" : null,
            ReportedAt = ReportedAt,
            OriginatingEndpoint = new EndpointDetails
            {
                Host = "localhost",
                HostId = Guid.Parse("55D0800D-CC90-47C3-83EB-DDE292140C28"),
                Name = "test-host"
            }
        });

        await CompleteDatabaseOperation();
    }
}
