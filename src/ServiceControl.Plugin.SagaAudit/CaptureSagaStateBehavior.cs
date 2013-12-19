namespace ServiceControl.EndpointPlugin.SagaState
{
    using System;
    using Messages.SagaState;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Saga;
    using NServiceBus.Sagas;
    using NServiceBus.Unicast.Messages;
    using Operations.ServiceControlBackend;

    // ReSharper disable CSharpWarnings::CS0618
    class CaptureSagaStateBehavior : IBehavior<HandlerInvocationContext>
    {
        public ServiceControlBackend ServiceControlBackend { get; set; }
        SagaUpdatedMessage sagaAudit;

        public void Invoke(HandlerInvocationContext context, Action next)
        {
            sagaAudit = new SagaUpdatedMessage
                {
                    StartTime = DateTime.UtcNow
                };
            context.Set(sagaAudit);
            next();
            sagaAudit.FinishTime = DateTime.UtcNow;
            var saga = context.MessageHandler.Instance as ISaga;
            if (saga != null)
            {
                AuditSaga(saga, context);
            }
        }

        void AuditSaga(ISaga saga, HandlerInvocationContext context)
        {
            //only support physical messages for now
            if (context.PhysicalMessage == null)
            {
                return;
            }
            var activeSagaInstance = context.Get<ActiveSagaInstance>();
            var sagaStateString = Serializer.Serialize(saga.Entity);
            var headers = context.LogicalMessage.Headers;
            var originatingMachine = headers[Headers.OriginatingMachine];
            var originatingEndpoint = headers[Headers.OriginatingEndpoint];
            var timeSent = DateTimeExtensions.ToUtcDateTime(headers[Headers.TimeSent]);

            sagaAudit.Initiator = new SagaChangeInitiator
                {
                    IsSagaTimeoutMessage = IsTimeoutMessage(context.LogicalMessage),
                    InitiatingMessageId = context.PhysicalMessage.Id,
                    OriginatingMachine = originatingMachine,
                    OriginatingEndpoint = originatingEndpoint,
                    MessageType = context.LogicalMessage.GetType().Name,
                    TimeSent = timeSent,
                };
            sagaAudit.IsNew = activeSagaInstance.IsNew;
            sagaAudit.SagaId = saga.Entity.Id;
            sagaAudit.SagaState = sagaStateString;
            ServiceControlBackend.Send(sagaAudit);
        }

        static bool IsTimeoutMessage(LogicalMessage message)
        {
            string isTimeoutString;
            if (message.Headers.TryGetValue(Headers.IsSagaTimeoutMessage, out isTimeoutString))
            {
                return isTimeoutString.ToLowerInvariant() == "true";
            }
            return false;
        }
    }
}