namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Implementation.Audit;
using ServiceControl.Persistence.Infrastructure;

// A primary whose audit data lives on a dedicated audit host carries the audit tables, empty, and
// must neither query nor sweep them. The setting is flipped after setup so the rows the tests seed
// are the ones a misconfiguration would have left behind.
class RemoteAuditDataTests : AuditRetentionTestBase
{
    [SetUp]
    public void AuditIsRemote() => EFSettings.HostsAuditData = false;

    [Test]
    public async Task The_message_views_read_only_the_failed_messages()
    {
        var failure = new IngestedFailure();
        var audit = new IngestedAudit();

        await Ingest(failure);
        await IngestAudit(audit);

        var result = await MessagesViewStore.GetAllMessages(new PagingInfo(), new SortInfo(), includeSystemMessages: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results.Select(view => view.Id), Is.EqualTo(new[] { failure.UniqueMessageIdString }));
            Assert.That(result.QueryStats.TotalCount, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Bodies_are_not_resolved_from_the_audit_table()
    {
        var audit = new IngestedAudit();

        await IngestAudit(audit);

        var result = await BodyStorage.TryFetch(audit.UniqueMessageIdString);

        Assert.That(result.State, Is.EqualTo(Operations.BodyStorage.MessageBodyState.NotFound));
    }

    [Test]
    public async Task The_retention_sweep_leaves_the_audit_tables_alone()
    {
        var expired = await SeedHour(AuditHours.Truncate(Now - Retention).AddHours(-1));

        await RunRetentionSweep();

        Assert.That(await HourIsGone(expired), Is.False);
    }
}
