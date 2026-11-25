namespace ServiceControl.SagaTimeouts
{
    using System.Threading.Tasks;
    using NServiceBus;
    using ServiceControl.Persistence.SagaTimeouts;

    [Handler]
    class SagaTimeoutReportHandler(SagaTimeoutReportProcessor processor) :
        IHandleMessages<SagaTimeoutRequested>,
        IHandleMessages<SagaTimeoutRescheduled>,
        IHandleMessages<SagaTimeoutCanceled>,
        IHandleMessages<SagaTimeoutTriggered>,
        IHandleMessages<SagaTimeoutChangeRejected>,
        IHandleMessages<SagaCompleted>
    {
        public Task Handle(SagaTimeoutRequested message, IMessageHandlerContext context) =>
            processor.Apply(message, SagaTimeoutChangeKind.Requested, change => change with
            {
                TriggerId = message.TriggerId,
                DueTime = message.DueTime,
                PayloadContentType = message.PayloadContentType,
                Payload = message.Payload
            }, context.CancellationToken);

        public Task Handle(SagaTimeoutRescheduled message, IMessageHandlerContext context) =>
            processor.Apply(message, SagaTimeoutChangeKind.Rescheduled, change => change with
            {
                TriggerId = message.TriggerId,
                DueTime = message.DueTime,
                PayloadContentType = message.PayloadContentType,
                Payload = message.Payload,
                OperationId = message.OperationId
            }, context.CancellationToken);

        public Task Handle(SagaTimeoutCanceled message, IMessageHandlerContext context) =>
            processor.Apply(message, SagaTimeoutChangeKind.Canceled, change => change with { OperationId = message.OperationId }, context.CancellationToken);

        public Task Handle(SagaTimeoutTriggered message, IMessageHandlerContext context) =>
            processor.Apply(message, SagaTimeoutChangeKind.Triggered, change => change with { TriggerId = message.TriggerId }, context.CancellationToken);

        public Task Handle(SagaTimeoutChangeRejected message, IMessageHandlerContext context) =>
            processor.ApplyRejection(message, context.CancellationToken);

        public Task Handle(SagaCompleted message, IMessageHandlerContext context) =>
            processor.ApplySagaCompleted(message, context.CancellationToken);
    }
}
