namespace ServiceControl.Persistence.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NUnit.Framework;
using ServiceControl.MessageFailures;
using ServiceControl.Persistence.EFCore.Abstractions;
using ServiceControl.Persistence.EFCore.Implementation.UnitOfWork;
using ServiceControl.Persistence.Infrastructure;

// The unit of work is never completed or disposed, so the database, scope and dialect are unused.
[TestFixture]
class EFRecoverabilityIngestionUnitOfWorkTests
{
    const string RetryHeader = "ServiceControl.Retry.UniqueMessageId";

    InMemoryBodyStoragePersistence storage;
    EFRecoverabilityIngestionUnitOfWork unitOfWork;

    [SetUp]
    public void SetUp()
    {
        storage = new InMemoryBodyStoragePersistence();
        var settings = new TestSettings { ConnectionString = "unused", BodyStorage = new FileSystemBodyStorageSettings { StoragePath = "unused" } };
        var parent = new EFIngestionUnitOfWork(null, null, storage, settings, null, TimeProvider.System);
        unitOfWork = new EFRecoverabilityIngestionUnitOfWork(parent, storage, settings);
    }

    async Task<string> Record(string retryHeaderValue)
    {
        var headers = new Dictionary<string, string>
        {
            [Headers.MessageId] = Guid.NewGuid().ToString(),
            [Headers.ProcessingEndpoint] = "Receiver",
            [Headers.ContentType] = "application/octet-stream"
        };
        if (retryHeaderValue is not null)
        {
            headers[RetryHeader] = retryHeaderValue;
        }

        var context = new MessageContext("native", headers, new byte[] { 1, 2, 3 }, new TransportTransaction(), "receiver", new ContextBag());
        var attempt = new FailedMessage.ProcessingAttempt { Headers = headers };

        await unitOfWork.RecordFailedProcessingAttempt(context, attempt, []);

        return storage.Written[^1].BodyId;
    }

    [TestCase("../traversal")]
    [TestCase("not a guid")]
    public async Task A_malformed_retry_header_stores_the_body_under_a_deterministic_guid(string value)
    {
        var first = await Record(value);
        var second = await Record(value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Guid.TryParse(first, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(DeterministicGuid.MakeId(value)));
            Assert.That(second, Is.EqualTo(first));
        }
    }

    [Test]
    public async Task A_valid_noncanonical_guid_is_stored_in_canonical_form()
    {
        var guid = Guid.NewGuid();

        var bodyId = await Record(guid.ToString("N").ToUpperInvariant());

        Assert.That(bodyId, Is.EqualTo(guid.ToString()));
    }

    [Test]
    public async Task A_message_without_a_retry_header_stores_the_body_under_its_derived_guid()
    {
        var bodyId = await Record(null);

        Assert.That(Guid.TryParse(bodyId, out _), Is.True);
    }

    sealed class TestSettings : EFPersisterSettings
    {
    }
}
