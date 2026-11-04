namespace ServiceControl.Persistence.Tests.RavenDB.DataMigration;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using ServiceControl.Persistence.DataMigration;

[TestFixture]
class FailedMessageBodySourceTests : RavenMigrationSourceTestBase
{
    static readonly MigrationCategory FailedMessages =
        MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.UnresolvedAndRetryIssuedFailedMessages);

    [Test]
    public async Task Reads_the_attachment_and_its_own_content_type()
    {
        var content = Encoding.UTF8.GetBytes("<order>1</order>");
        var documentId = await SeedWithAttachment(content, "application/json");

        await using var source = await OpenMigrationSource();
        var body = await source.ReadBody(FailedMessages, documentId);

        Assert.That(body, Is.Not.Null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.Content.ToArray(), Is.EqualTo(content));
            Assert.That(body.ContentType, Is.EqualTo("application/json"), "the message header says text/xml, the attachment's own type wins");
        }
    }

    [Test]
    public async Task Reads_a_zero_length_attachment_as_an_empty_body()
    {
        var documentId = await SeedWithAttachment([], "text/plain");

        await using var source = await OpenMigrationSource();
        var body = await source.ReadBody(FailedMessages, documentId);

        Assert.That(body, Is.Not.Null, "an empty payload is a body, and is not the same as a missing one");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(body.Content.Length, Is.Zero);
            Assert.That(body.ContentType, Is.EqualTo("text/plain"));
        }
    }

    [Test]
    public async Task Returns_null_when_the_document_has_no_attachment()
    {
        var message = await SeedFailedMessage(new IngestedFailure().ToFailedMessage());

        await using var source = await OpenMigrationSource();

        Assert.That(await source.ReadBody(FailedMessages, message.Id), Is.Null);
    }

    [Test]
    public async Task Returns_null_for_a_document_that_does_not_exist()
    {
        await using var source = await OpenMigrationSource();

        Assert.That(await source.ReadBody(FailedMessages, "FailedMessages/no-such-message"), Is.Null,
            "a message RavenDB expired during the copy has to be a BodyMissing skip, not three failed reads and a BodyUnreadable one");
    }

    [Test]
    public async Task Refuses_a_category_it_does_not_read_body_attachments_for()
    {
        var message = await SeedFailedMessage(new IngestedFailure().ToFailedMessage());
        var failedErrorImports = MigrationCategoryRegistry.All.Single(category => category.Id == MigrationCategoryIds.FailedErrorImports);

        await using var source = await OpenMigrationSource();

        var exception = Assert.ThrowsAsync<NotSupportedException>(() => source.ReadBody(failedErrorImports, message.Id),
            "a failed error import keeps its body inside the document, so reading an attachment would skip every one of them as BodyMissing");
        Assert.That(exception.Message, Does.Contain(MigrationCategoryIds.FailedErrorImports));
    }

    async Task<string> SeedWithAttachment(byte[] content, string contentType)
    {
        var message = await SeedFailedMessage(new IngestedFailure().ToFailedMessage());

        using var session = await SessionProvider.OpenSession();
        session.Advanced.Attachments.Store(message.Id, "body", new MemoryStream(content), contentType);
        await session.SaveChangesAsync();

        return message.Id;
    }
}
