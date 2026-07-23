namespace ServiceControl.Persistence.Tests;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

// Bodies are always stored. MaxBodySizeToStore only decides whether the body lives inline in
// BodyText or in external storage with at most a search prefix left inline.
class ErrorIngestionBodyTests : ErrorIngestionTestBase
{
    const int Cap = 64;

    [SetUp]
    public void ShrinkTheBodyCap() => EFSettings.MaxBodySizeToStore = Cap;

    [Test]
    public async Task Text_within_the_cap_is_stored_inline()
    {
        var failure = new IngestedFailure { Body = Encoding.UTF8.GetBytes("<order>1</order>") };

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.EqualTo("<order>1</order>"));
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(row.BodySize, Is.EqualTo(failure.Body.Length));
        }

        Assert.That(RecordedBodies.Written, Is.Empty);
    }

    [Test]
    public async Task Text_over_the_cap_goes_external_and_leaves_a_search_prefix()
    {
        // Two byte characters, so the cap falls in the middle of one
        var body = Encoding.UTF8.GetBytes(new string('é', Cap));
        var failure = new IngestedFailure { Body = body };

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        Assert.That(row.BodyStoredExternally, Is.True);
        Assert.That(row.BodyText, Is.Not.Null);

        var prefixBytes = Encoding.UTF8.GetByteCount(row.BodyText);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(prefixBytes, Is.LessThanOrEqualTo(Cap), "The prefix must not exceed the cap");
            Assert.That(row.BodyText, Is.EqualTo(new string('é', Cap / 2)), "The prefix must end on a character boundary");
            Assert.That(row.BodySize, Is.EqualTo(body.Length), "BodySize is the original size");
        }

        var written = RecordedBodies.Written.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(written.BodyId, Is.EqualTo(failure.UniqueMessageIdString));
            Assert.That(written.Body, Is.EqualTo(body), "External storage holds the whole body");
            Assert.That(written.ContentType, Is.EqualTo(failure.ContentType));
        }
    }

    [Test]
    public async Task A_binary_body_goes_external_whatever_its_size()
    {
        var failure = new IngestedFailure
        {
            ContentType = "application/octet-stream",
            Body = BitConverter.GetBytes(0xDEADBEEF)
        };

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Null);
            Assert.That(row.BodyStoredExternally, Is.True);
            Assert.That(row.BodySize, Is.EqualTo(failure.Body.Length));
        }

        Assert.That(RecordedBodies.Written.Single().Body, Is.EqualTo(failure.Body));
    }

    [Test]
    public async Task A_body_that_is_not_valid_utf8_goes_external()
    {
        var failure = new IngestedFailure { Body = [0xFF, 0xFE, 0xFD] };

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Null);
            Assert.That(row.BodyStoredExternally, Is.True);
        }
    }

    [Test]
    public async Task A_body_containing_a_nul_goes_external()
    {
        var failure = new IngestedFailure { Body = Encoding.UTF8.GetBytes("abc\0def") };

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Null);
            Assert.That(row.BodyStoredExternally, Is.True);
        }
    }

    [Test]
    public async Task An_empty_body_is_stored_nowhere()
    {
        var failure = new IngestedFailure { Body = [] };

        await Ingest(failure);

        var row = await GetFailedMessage(failure.UniqueMessageId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.BodyText, Is.Null);
            Assert.That(row.BodyStoredExternally, Is.False);
            Assert.That(row.BodySize, Is.Zero);
        }

        Assert.That(RecordedBodies.Written, Is.Empty);
    }
}
