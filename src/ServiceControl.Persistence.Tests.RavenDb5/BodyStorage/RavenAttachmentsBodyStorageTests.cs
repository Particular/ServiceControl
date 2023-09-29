namespace ServiceControl.UnitTests.BodyStorage
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Remoting.Contexts;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Transport;
    using NUnit.Framework;
    using ServiceControl.MessageFailures;
    using ServiceControl.Persistence.UnitOfWork;

    [TestFixture]
    sealed class RavenAttachmentsBodyStorageTests : PersistenceTestBase
    {
        [Test]
        public async Task Attachments_with_ids_that_contain_backslash_should_be_readable()
        {
            var messageId = "3f0240a7-9b2e-4e2a-ab39-6114932adad1\\2055783";
            var contentType = "NotImportant";
            var endpointName = "EndpointName";
            var body = BitConverter.GetBytes(0xDEADBEEF);
            var ingestionFactory = GetRequiredService<IIngestionUnitOfWorkFactory>();

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = messageId,
                [Headers.ProcessingEndpoint] = endpointName,
                [Headers.ContentType] = contentType
            };

            using (var cancellationSource = new CancellationTokenSource())
            using (var uow = await ingestionFactory.StartNew())
            {
                var context = new MessageContext(messageId, headers, body, new TransportTransaction(), cancellationSource, new NServiceBus.Extensibility.ContextBag());
                var processingAttempt = new FailedMessage.ProcessingAttempt
                {
                    MessageId = messageId,
                    FailureDetails = new Contracts.Operations.FailureDetails
                    {
                        AddressOfFailingEndpoint = endpointName
                    },
                    Headers = headers
                };
                var groups = new List<FailedMessage.FailureGroup>();

                await uow.Recoverability.RecordFailedProcessingAttempt(context, processingAttempt, groups);
                await uow.Complete();
            }

            var uniqueMessageId = headers.UniqueId();

            var retrieved = await BodyStorage.TryFetch(uniqueMessageId);
            Assert.IsNotNull(retrieved);
            Assert.True(retrieved.HasResult);
            Assert.AreEqual(contentType, retrieved.ContentType);

            var buffer = new byte[retrieved.BodySize];
            retrieved.Stream.Read(buffer, 0, retrieved.BodySize);

            Assert.AreEqual(body, buffer);
        }
    }
}