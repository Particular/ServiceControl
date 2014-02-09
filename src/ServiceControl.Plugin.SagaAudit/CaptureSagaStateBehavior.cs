namespace ServiceControl.Plugin.SagaAudit
{
    using System;
    using EndpointPlugin.Messages.SagaState;
    using EndpointPlugin.Operations.ServiceControlBackend;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Saga;
    using NServiceBus.Sagas;
    using NServiceBus.Unicast.Messages;

    // ReSharper disable CSharpWarnings::CS0618
    class CaptureSagaStateBehavior : IBehavior<HandlerInvocationContext>
    {
        public ServiceControlBackend ServiceControlBackend { get; set; }

        public void Invoke(HandlerInvocationContext context, Action next)
        {
            var saga = context.MessageHandler.Instance as ISaga;

            if (saga == null)
            {
                next();
                return;
            }

            sagaAudit = new SagaUpdatedMessage
            {
                StartTime = DateTime.UtcNow
            };
            context.Set(sagaAudit);
            next();

            if (saga.Entity == null)
            {
                return; // Message was not handled by the saga
            }

            sagaAudit.FinishTime = DateTime.UtcNow;
            AuditSaga(saga, context);
        }

        void AuditSaga(ISaga saga, HandlerInvocationContext context)
        {
            string messageId;

            if (!context.LogicalMessage.Headers.TryGetValue(Headers.MessageId, out messageId))
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
                InitiatingMessageId = messageId,
                OriginatingMachine = originatingMachine,
                OriginatingEndpoint = originatingEndpoint,
                MessageType = context.LogicalMessage.MessageType.FullName,
                TimeSent = timeSent,
            };
            sagaAudit.IsNew = activeSagaInstance.IsNew;
            sagaAudit.IsCompleted = saga.Completed;
            sagaAudit.Endpoint = Configure.EndpointName;
            sagaAudit.SagaId = saga.Entity.Id;
            sagaAudit.SagaType = saga.GetType().FullName;
            sagaAudit.SagaState = sagaStateString;
            ServiceControlBackend.Send(sagaAudit);

            AssignSagaStateChangeCausedByMessage(context);
        }

        void AssignSagaStateChangeCausedByMessage(BehaviorContext context)
        {
            var physicalMessage = context.Get<TransportMessage>(ReceivePhysicalMessageContext.IncomingPhysicalMessageKey);
            string sagaStateChange;

            if (!physicalMessage.Headers.TryGetValue("ServiceControl.SagaStateChange", out sagaStateChange))
            {
                sagaStateChange = String.Empty;
            }

            var statechange = "updated";
            if (sagaAudit.IsNew)
            {
                statechange = "initiated";
            }
            if (sagaAudit.IsCompleted)
            {
                statechange = "completed";
            }

            if (!String.IsNullOrEmpty(sagaStateChange))
            {
                sagaStateChange += ";";
            }
            sagaStateChange += String.Format("{0}:{1}", sagaAudit.SagaId, statechange);

            physicalMessage.Headers["ServiceControl.SagaStateChange"] = sagaStateChange;
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

        SagaUpdatedMessage sagaAudit;
    }
}