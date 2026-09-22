namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Implementation.Audit;

// Bodies are always stored. MaxBodySizeToStore only decides whether the body lives inline in
// BodyText or in external storage, under a key that names the row's ingestion hour.
class AuditIngestionBodyTests : AuditIngestionTestBase
{
    const int Cap = 64;

    [SetUp]
    public void ShrinkTheBodyCap() => EFSettings.BodyStorage.MaxBodySizeToStore = Cap;

    [Test]
    public async Task Text_within_the_cap_is_stored_inline()
    {
        var audit = new IngestedAudit { Body = Encoding.UTF8.GetBytes("<order>1</order>") };

        await IngestAudit(audit);

        var row = await GetAuditMessage(audit.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.EqualTo("<order>1</order>"));
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(row.BodySize, Is.EqualTo(audit.Body.Length));
        }

        Assert.That(RecordedBodies.Written, Is.Empty);
    }

    [Test]
    public async Task Text_over_the_cap_goes_external_under_the_ingestion_hour()
    {
        var body = Encoding.UTF8.GetBytes(new string('x', Cap * 2));
        var audit = new IngestedAudit { Body = body };

        await IngestAudit(audit);

        var row = await GetAuditMessage(audit.UniqueMessageId);
        var written = RecordedBodies.Written.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyStoredExternally, Is.True);
            Assert.That(row.BodyText, Is.EqualTo(new string('x', Cap)), "A search prefix stays inline");
            Assert.That(row.BodySize, Is.EqualTo(body.Length));
            Assert.That(written.BodyId, Is.EqualTo(AuditBodyStorage.BodyId(row.CreatedOn, audit.UniqueMessageId)));
            Assert.That(written.BodyId, Does.StartWith($"audit/{row.CreatedOn:yyyy-MM-dd-HH}/"));
            Assert.That(written.Body, Is.EqualTo(body));
            Assert.That(written.ContentType, Is.EqualTo(audit.ContentType));
        }
    }

    [Test]
    public async Task A_binary_body_goes_external_whatever_its_size()
    {
        var audit = new IngestedAudit { ContentType = "application/octet-stream", Body = BitConverter.GetBytes(0xDEADBEEF) };

        await IngestAudit(audit);

        var row = await GetAuditMessage(audit.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyStoredExternally, Is.True);
            Assert.That(row.BodyText, Is.Null);
            Assert.That(RecordedBodies.Written.Single().BodyId, Is.EqualTo(AuditBodyStorage.BodyId(row.CreatedOn, audit.UniqueMessageId)));
        }
    }

    [Test]
    public async Task An_empty_body_is_neither_stored_inline_nor_externally()
    {
        var audit = new IngestedAudit { Body = [] };

        await IngestAudit(audit);

        var row = await GetAuditMessage(audit.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Null);
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(row.BodySize, Is.Zero);
            Assert.That(RecordedBodies.Written, Is.Empty);
        }
    }
}
