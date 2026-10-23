namespace ServiceControl.SagaTimeouts
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using System.Threading.Tasks;
    using Infrastructure.Auth;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Transport;
    using ServiceControl.Persistence.SagaTimeouts;

    [Handler]
    class RequestSagaTimeoutChangeHandler(IMessageDispatcher dispatcher, IMessageActionAuditLog auditLog) : IHandleMessages<RequestSagaTimeoutChange>
    {
        public async Task Handle(RequestSagaTimeoutChange message, IMessageHandlerContext context)
        {
            var request = BuildRequest(message);
            var transportTransaction = context.Extensions.GetOrCreate<TransportTransaction>();

            await dispatcher.Dispatch(
                new TransportOperations(new TransportOperation(request, new UnicastAddressTag(message.ManagementAddress))),
                transportTransaction,
                context.CancellationToken);

            var (user, operationId) = AuditHeaders.Read(context.MessageHeaders);
            if (!string.IsNullOrEmpty(operationId))
            {
                auditLog.MessageAction(user, ToAuditKind(message.Change), Permissions.ErrorTimeoutsManage, MessageActionScope.Single, message.TimeoutId, operationId);
            }
        }

        // The satellite does not deserialize the body, so every field of the request travels in a header
        public static OutgoingMessage BuildRequest(RequestSagaTimeoutChange message)
        {
            var messageId = Guid.CreateVersion7().ToString("D");
            var isCancel = message.Change == SagaTimeoutOperation.Cancel;

            var headers = new Dictionary<string, string>
            {
                [Headers.MessageId] = messageId,
                [ManagementHeaders.OperationId] = message.OperationId.ToString("D"),
                [ManagementHeaders.SagaType] = message.SagaType,
                [ManagementHeaders.SagaId] = message.SagaId.ToString("D"),
                [ManagementHeaders.TimeoutId] = message.TimeoutId,
                [ManagementHeaders.ExpectedVersion] = message.ExpectedVersion.ToString(CultureInfo.InvariantCulture),
                [ManagementHeaders.Change] = isCancel ? ManagementHeaders.CancelChange : ManagementHeaders.RescheduleChange
            };

            if (!isCancel)
            {
                var dueTime = message.DueTime ?? throw new InvalidOperationException($"A {message.Change} request for timeout {message.TimeoutId} has no due time.");
                headers[ManagementHeaders.DueTime] = DateTimeOffsetHelper.ToWireFormattedString(dueTime);
            }

            var body = Array.Empty<byte>();
            if (message.Change == SagaTimeoutOperation.Reschedule && message.Payload != null)
            {
                body = Encoding.UTF8.GetBytes(message.Payload);
                headers[Headers.ContentType] = message.PayloadContentType ?? "application/json";
            }

            return new OutgoingMessage(messageId, headers, body);
        }

        static MessageActionKind ToAuditKind(SagaTimeoutOperation change) => change switch
        {
            SagaTimeoutOperation.Cancel => MessageActionKind.Cancel,
            SagaTimeoutOperation.Trigger => MessageActionKind.Trigger,
            SagaTimeoutOperation.Reschedule => MessageActionKind.Reschedule,
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null)
        };

        public static class ManagementHeaders
        {
            public const string OperationId = "NServiceBus.SagaTimeoutManagement.OperationId";
            public const string SagaType = "NServiceBus.SagaTimeoutManagement.SagaType";
            public const string SagaId = "NServiceBus.SagaTimeoutManagement.SagaId";
            public const string TimeoutId = "NServiceBus.SagaTimeoutManagement.TimeoutId";
            public const string ExpectedVersion = "NServiceBus.SagaTimeoutManagement.ExpectedVersion";
            public const string Change = "NServiceBus.SagaTimeoutManagement.Change";
            public const string DueTime = "NServiceBus.SagaTimeoutManagement.DueTime";

            public const string RescheduleChange = "Reschedule";
            public const string CancelChange = "Cancel";
        }
    }
}
