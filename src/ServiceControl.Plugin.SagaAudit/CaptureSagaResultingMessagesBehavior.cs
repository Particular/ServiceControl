namespace ServiceControl.Plugin.SagaAudit
{
    using System;
    using System.Linq;
    using EndpointPlugin.Messages.SagaState;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    // ReSharper disable CSharpWarnings::CS0618
    class CaptureSagaResultingMessagesBehavior : IBehavior<SendPhysicalMessageContext>
    {

        static ILog logger = LogManager.GetLogger(typeof(CaptureSagaResultingMessagesBehavior));
        SagaUpdatedMessage sagaUpdatedMessage;

        public void Invoke(SendPhysicalMessageContext context, Action next)
        {
            AppendMessageToState(context);
            next();
        }

        void AppendMessageToState(SendPhysicalMessageContext context)
        {
            if (!context.TryGet(out sagaUpdatedMessage))
            {
                return;
            }
            var logicalMessage = context.LogicalMessage;
            if (logicalMessage == null)
            {
                //this can happen on control messages
                return;
            }
            
            var sagaResultingMessage = new SagaChangeOutput
                {
                    ResultingMessageId = context.MessageToSend.Id,
                    TimeSent = DateTimeExtensions.ToUtcDateTime(context.MessageToSend.Headers[Headers.TimeSent]),
                    MessageType = logicalMessage.MessageType.ToString(),
                    DeliveryDelay = context.SendOptions.DelayDeliveryWith,
                    DeliveryAt = context.SendOptions.DeliverAt,
                    Destination = GetDestination(context),
                    Intent = context.SendOptions.Intent.ToString()
                };
            sagaUpdatedMessage.ResultingMessages.Add(sagaResultingMessage);
        }

        static string GetDestination(SendPhysicalMessageContext context)
        {
            // Destination can be null for publish events
            if (context.SendOptions.Destination != null)
            {
                return context.SendOptions.Destination.ToString();
            }
            return null;
        }
    }
}