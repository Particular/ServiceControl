namespace ServiceControl.EndpointPlugin.SagaState
{
    using System;
    using System.Diagnostics;
    using Messages.SagaState;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;
    using NServiceBus.Saga;
    using NServiceBus.Sagas;
    using Operations.ServiceControlBackend;

    // ReSharper disable CSharpWarnings::CS0618
    class CaptureSagaStateBehavior : IBehavior<HandlerInvocationContext>
    {
        public ServiceControlBackend ServiceControlBackend { get; set; }
        DateTime startTime;
        DateTime finishTime;

        public void Invoke(HandlerInvocationContext context, Action next)
        {
            Debug.WriteLine("CaptureSagaStateBehavior");
            startTime = DateTime.UtcNow;
            next();
            finishTime = DateTime.UtcNow;
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
            var originatingMachine = context.LogicalMessage.Headers[Headers.OriginatingMachine];
            var originatingEndpoint = context.LogicalMessage.Headers[Headers.OriginatingEndpoint];
            var timeSent = context.LogicalMessage.Headers[Headers.TimeSent];

            var sagaAudit = new SagaUpdatedMessage
                {
                    SagaState = sagaStateString,
                    StartTime = startTime,
                    FinishTime = finishTime,
                    SagaId = saga.Entity.Id,
                    IsNew = activeSagaInstance.IsNew,
                    Initiator = new SagaChangeInitiator
                        {
                            InitiatingMessageId = context.PhysicalMessage.Id,
                            OriginatingMachine = originatingMachine,
                            OriginatingEndpoint = originatingEndpoint,
                            MessageType = context.LogicalMessage.GetType().Name,
                            TimeSent = timeSent,
                        },
                };
            context.ParentContext.ParentContext.Set(sagaAudit);
            ServiceControlBackend.Send(sagaAudit);
        }

    }
}