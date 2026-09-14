namespace ServiceControl.Persistence.Tests;

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Operations.BodyStorage;

class AuditBodyStorageTests : AuditIngestionTestBase
{
    const int Cap = 64;

    [SetUp]
    public void ShrinkTheBodyCap() => EFSettings.BodyStorage.MaxBodySizeToStore = Cap;

    [Test]
    public async Task Fetches_an_inline_audited_body()
    {
        var audit = new IngestedAudit { Body = Encoding.UTF8.GetBytes("<order>1</order>") };
        await IngestAudit(audit);

        var result = await BodyStorage.TryFetch(audit.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.State, Is.EqualTo(MessageBodyState.Available));
            Assert.That(result.Content.ContentType, Is.EqualTo(audit.ContentType));
            Assert.That(Encoding.UTF8.GetString(ReadAll(result.Content.Stream)), Is.EqualTo("<order>1</order>"));
        }
    }

    [Test]
    public async Task Fetches_an_external_audited_body_from_its_hour()
    {
        var body = Encoding.UTF8.GetBytes(new string('a', Cap * 2));
        var audit = new IngestedAudit { Body = body };
        await IngestAudit(audit);

        var result = await BodyStorage.TryFetch(audit.UniqueMessageIdString);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.State, Is.EqualTo(MessageBodyState.Available));
            Assert.That(ReadAll(result.Content.Stream), Is.EqualTo(body), "the whole body, not the inline search prefix");
        }
    }

    [Test]
    public async Task A_failed_messages_body_wins_over_its_audit_rows()
    {
        var failure = new IngestedFailure { Body = Encoding.UTF8.GetBytes("<failed/>") };
        await Ingest(failure);
        await IngestAudit(new IngestedAudit { RetryOf = failure.UniqueMessageIdString, Body = Encoding.UTF8.GetBytes("<audited/>") });

        var result = await BodyStorage.TryFetch(failure.UniqueMessageIdString);

        Assert.That(Encoding.UTF8.GetString(ReadAll(result.Content.Stream)), Is.EqualTo("<failed/>"));
    }

    [Test]
    public async Task An_unknown_id_is_not_found()
    {
        await IngestAudit(new IngestedAudit());

        var result = await BodyStorage.TryFetch(Guid.NewGuid().ToString());

        Assert.That(result.State, Is.EqualTo(MessageBodyState.NotFound));
    }

    static byte[] ReadAll(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
