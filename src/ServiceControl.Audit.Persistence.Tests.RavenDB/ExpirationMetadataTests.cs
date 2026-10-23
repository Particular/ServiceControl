namespace ServiceControl.Audit.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Auditing;
using NUnit.Framework;
using Raven.Client;
using SagaAudit;

[TestFixture]
class ExpirationMetadataTests : PersistenceTestFixture
{
    static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    public override Task Setup()
    {
        SetSettings = s => s.AuditRetentionPeriod = RetentionPeriod;
        return base.Setup();
    }

    [Test]
    public async Task Processed_message_should_expire_with_its_body_after_the_retention_period()
    {
        var message = AuditTestData.CreateProcessedMessage();

        await using (var unitOfWork = await StartAuditUnitOfWork(1))
        {
            await unitOfWork.RecordProcessedMessage(message, Encoding.UTF8.GetBytes("The body"));
            await unitOfWork.Complete();
        }

        using var session = configuration.DocumentStore.OpenAsyncSession();
        var document = await session.LoadAsync<ProcessedMessage>(message.Id, TestContext.CurrentContext.CancellationToken);
        var metadata = session.Advanced.GetMetadataFor(document);

        using (Assert.EnterMultipleScope())
        {
            AssertExpiresAfterRetentionPeriod(metadata);
            Assert.That(await session.Advanced.Attachments.ExistsAsync(message.Id, "body", TestContext.CurrentContext.CancellationToken), Is.True, "The body is an attachment of the message document, so it expires with it");
        }
    }

    [Test]
    public async Task Saga_snapshot_should_expire_after_the_retention_period()
    {
        var snapshot = new SagaSnapshot { SagaId = Guid.NewGuid() };

        await using (var unitOfWork = await StartAuditUnitOfWork(1))
        {
            await unitOfWork.RecordSagaSnapshot(snapshot);
            await unitOfWork.Complete();
        }

        using var session = configuration.DocumentStore.OpenAsyncSession();
        var document = await session.LoadAsync<SagaSnapshot>(snapshot.Id, TestContext.CurrentContext.CancellationToken);

        AssertExpiresAfterRetentionPeriod(session.Advanced.GetMetadataFor(document));
    }

    static void AssertExpiresAfterRetentionPeriod(IDictionary<string, object> metadata)
    {
        Assert.That(metadata.TryGetValue(Constants.Documents.Metadata.Expires, out var expiresValue), Is.True, $"{Constants.Documents.Metadata.Expires} is missing from the document metadata");

        var expires = DateTime.Parse((string)expiresValue, null, DateTimeStyles.RoundtripKind);

        Assert.That(expires, Is.EqualTo(DateTime.UtcNow.Add(RetentionPeriod)).Within(TimeSpan.FromMinutes(5)));
    }
}
