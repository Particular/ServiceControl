namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Auditing;
    using Microsoft.Extensions.DependencyInjection;
    using Monitoring;
    using NServiceBus;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;

    abstract class EFPersistenceTestFixture : PersistenceTestFixture
    {
        protected async Task Ingest(params ProcessedMessage[] messages)
        {
            await using var unitOfWork = await StartAuditUnitOfWork(messages.Length);
            foreach (var message in messages)
            {
                await unitOfWork.RecordProcessedMessage(message);
            }

            await unitOfWork.Complete();
            await configuration.CompleteDBOperation();
        }

        protected async Task Ingest(ProcessedMessage message, byte[] body)
        {
            await using var unitOfWork = await StartAuditUnitOfWork(1);
            await unitOfWork.RecordProcessedMessage(message, body);
            await unitOfWork.Complete();
            await configuration.CompleteDBOperation();
        }

        protected async Task<T> WithDbContext<T>(Func<AuditDbContext, CancellationToken, Task<T>> query)
        {
            await using var scope = ServiceProvider.CreateAsyncScope();
            return await query(scope.ServiceProvider.GetRequiredService<AuditDbContext>(), TestTimeoutCancellationToken);
        }

        protected static ProcessedMessage MakeMessage(
            string messageId = null,
            string receivingEndpoint = "Receiver",
            string contentType = "application/json",
            bool isRetried = false,
            Dictionary<string, string> extraHeaders = null,
            string conversationId = null)
        {
            messageId ??= Guid.NewGuid().ToString();
            conversationId ??= Guid.NewGuid().ToString();

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = messageId,
                [Headers.ProcessingEndpoint] = receivingEndpoint,
                [Headers.MessageIntent] = MessageIntent.Publish.ToString(),
                [Headers.ConversationId] = conversationId,
                [Headers.EnclosedMessageTypes] = MessageType
            };

            if (contentType != null)
            {
                headers[Headers.ContentType] = contentType;
            }

            foreach (var header in extraHeaders ?? [])
            {
                headers[header.Key] = header.Value;
            }

            var metadata = new Dictionary<string, object>
            {
                ["MessageId"] = messageId,
                ["MessageType"] = MessageType,
                ["TimeSent"] = TimeSent,
                ["ConversationId"] = conversationId,
                ["IsSystemMessage"] = false,
                ["IsRetried"] = isRetried,
                ["SendingEndpoint"] = new EndpointDetails { Name = "Sender", HostId = SenderHostId, Host = "sender-host" },
                ["ReceivingEndpoint"] = new EndpointDetails { Name = receivingEndpoint, HostId = ReceiverHostId, Host = "receiver-host" },
                ["CriticalTime"] = TimeSpan.FromSeconds(3),
                ["ProcessingTime"] = TimeSpan.FromSeconds(1),
                ["DeliveryTime"] = TimeSpan.FromSeconds(2)
            };

            return new ProcessedMessage(headers, metadata);
        }

        protected const string MessageType = "Shipping.OrderShipped";
        protected static readonly DateTime TimeSent = new(2026, 9, 1, 10, 30, 0, DateTimeKind.Utc);
        protected static readonly Guid SenderHostId = Guid.NewGuid();
        protected static readonly Guid ReceiverHostId = Guid.NewGuid();
    }
}
