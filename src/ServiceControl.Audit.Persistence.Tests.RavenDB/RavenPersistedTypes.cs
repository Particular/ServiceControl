namespace ServiceControl.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using Particular.Approvals;
    using Raven.Abstractions.Data;
    using Raven.Client.Connection.Async;
    using Raven.Client.Indexes;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Monitoring;
    using ServiceControl.Audit.Persistence.Infrastructure;
    using ServiceControl.Audit.Persistence.Monitoring;
    using ServiceControl.Audit.Persistence.RavenDb;
    using ServiceControl.Audit.Persistence.Tests;

    class RavenPersistedTypes : PersistenceTestFixture
    {
        [Test]
        public void Verify()
        {
            var ravenPersistenceType = typeof(RavenDbPersistenceConfiguration);

            var ravenPersistenceTypes = ravenPersistenceType.Assembly.GetTypes()
                .Where(type => typeof(AbstractIndexCreationTask).IsAssignableFrom(type))
                .SelectMany(indexType => indexType.BaseType?.GenericTypeArguments)
                .Distinct();

            var documentTypeNames = string.Join(Environment.NewLine, ravenPersistenceTypes.Select(t => t.AssemblyQualifiedName));

            Approver.Verify(documentTypeNames);
        }

        [Test]
        public async Task CanLoadLegacyTypes()
        {
            var messageId = Guid.NewGuid().ToString();
            var endpointName = "Sales";
            var message = MakeMessage(messageId: messageId, processingEndpoint: endpointName);
            message.MessageMetadata.Add("ReceivingEndpoint", new EndpointDetails()
            {
                Host = "sender@machine",
                HostId = Guid.NewGuid(),
                Name = "Sender"
            });

            await IngestProcessedMessagesAudits(message);

            _ = await configuration.DocumentStore.AsyncDatabaseCommands.PatchAsync(message.Id, new ScriptedPatchRequest()
            {
                Script =
@"
var messageMetadata = this.MessageMetadata;
messageMetadata['ReceivingEndpoint']['$type'] = 'This.Is.The.Wrong.EndpointDetails, InTheWrongAssembly';
"
            });

            await configuration.CompleteDBOperation();

            var messages = await DataStore.GetMessages(false, new Audit.Infrastructure.PagingInfo(), new Audit.Infrastructure.SortInfo("Id", "asc"));

            Assert.That(messages, Is.Not.Null);
            Assert.That(messages.Results, Is.Not.Null);
            Assert.That(messages.Results.Single().Id, Is.EqualTo(message.UniqueMessageId));
        }

        ProcessedMessage MakeMessage(
            string messageId = null,
            MessageIntentEnum intent = MessageIntentEnum.Send,
            string conversationId = null,
            string processingEndpoint = null
        )
        {
            messageId = messageId ?? Guid.NewGuid().ToString();
            conversationId = conversationId ?? Guid.NewGuid().ToString();
            processingEndpoint = processingEndpoint ?? "SomeEndpoint";

            var metadata = new Dictionary<string, object>
            {
                { "MessageId", messageId },
                { "MessageIntent", intent },
                { "CriticalTime", TimeSpan.FromSeconds(5) },
                { "ProcessingTime", TimeSpan.FromSeconds(1) },
                { "DeliveryTime", TimeSpan.FromSeconds(4) },
                { "IsSystemMessage", false },
                { "MessageType", "MyMessageType" },
                { "IsRetried", false },
                { "ConversationId", conversationId },
                //{ "ContentLength", 10}
            };

            var headers = new Dictionary<string, string>
            {
                { Headers.MessageId, messageId },
                { Headers.ProcessingEndpoint, processingEndpoint },
                { Headers.MessageIntent, intent.ToString() },
                { Headers.ConversationId, conversationId }
            };


            return new ProcessedMessage(headers, metadata);
        }

        async Task IngestProcessedMessagesAudits(params ProcessedMessage[] processedMessages)
        {
            var unitOfWork = StartAuditUnitOfWork(processedMessages.Length);
            foreach (var processedMessage in processedMessages)
            {
                await unitOfWork.RecordProcessedMessage(processedMessage);
            }
            await unitOfWork.DisposeAsync();
            await configuration.CompleteDBOperation();
        }
    }
}