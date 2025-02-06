namespace ServiceControl.Persistence.Tests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Operations;
    using ServiceControl.Persistence.UnitOfWork;

    [TestFixture]
    sealed class AttachmentsBodyStorageTests : PersistenceTestBase
    {
        [Test]
        public async Task QueryByUniqueId()
        {
            await RunTest(headers => headers.UniqueId());
        }

        [Test]
        public async Task QueryByMessageId()
        {
            await RunTest(headers => headers.MessageId());
        }

        async Task RunTest(Func<Dictionary<string, string>, string> getIdToQuery)
        {
            // Contains a backslash, like an old MSMQ message id, to ensure that message ids like this are usable
            var messageId = "3f0240a7-9b2e-4e2a-ab39-6114932adad1\\2055783";
            var contentType = "NotImportant";
            var endpointName = "EndpointName";
            var body = BitConverter.GetBytes(0xDEADBEEF);
            var ingestionFactory = ServiceProvider.GetRequiredService<IIngestionUnitOfWorkFactory>();

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = messageId,
                [Headers.ProcessingEndpoint] = endpointName,
                [Headers.ContentType] = contentType
            };

            using (var cancellationSource = new CancellationTokenSource())
            using (var uow = await ingestionFactory.StartNew())
            {
                var context = new MessageContext(messageId, headers, body, new TransportTransaction(), "receiveAddress", new NServiceBus.Extensibility.ContextBag());
                var processingAttempt = new FailedMessage.ProcessingAttempt
                {
                    MessageId = messageId,
                    MessageMetadata = new Dictionary<string, object>
                    {
                        ["MessageId"] = messageId,
                        ["TimeSent"] = DateTime.UtcNow,
                        ["ReceivingEndpoint"] = new EndpointDetails
                        {
                            Name = endpointName,
                            Host = "Host",
                            HostId = Guid.NewGuid()
                        }
                    },
                    FailureDetails = new Contracts.Operations.FailureDetails
                    {
                        AddressOfFailingEndpoint = endpointName
                    },
                    Headers = headers
                };
                var groups = new List<FailedMessage.FailureGroup>();

                await uow.Recoverability.RecordFailedProcessingAttempt(context, processingAttempt, groups);
                await uow.Complete(cancellationSource.Token);
            }

            CompleteDatabaseOperation();

            var fetchById = getIdToQuery(headers);

            var retrieved = await BodyStorage.TryFetch(fetchById);
            Assert.That(retrieved, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(retrieved.HasResult, Is.True);
                Assert.That(retrieved.ContentType, Is.EqualTo(contentType));
            });

            var buffer = new byte[retrieved.BodySize];
            retrieved.Stream.Read(buffer, 0, retrieved.BodySize);

            Assert.That(buffer, Is.EqualTo(body));
        }
    }
}