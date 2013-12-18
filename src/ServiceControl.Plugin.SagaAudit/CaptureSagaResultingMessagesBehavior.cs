namespace ServiceControl.EndpointPlugin.SagaState
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Messages.SagaState;
    using NServiceBus;
    using NServiceBus.Pipeline;
    using NServiceBus.Pipeline.Contexts;

    // ReSharper disable CSharpWarnings::CS0618
    class CaptureSagaResultingMessagesBehavior : IBehavior<SendPhysicalMessageContext>
    {
        SagaUpdatedMessage sagaUpdatedMessage;

        public void Invoke(SendPhysicalMessageContext context, Action next)
        {
            Debug.WriteLine("CaptureSagaResultingMessagesBehavior");
            AppendMessageToState(context);
            next();
        }

        void AppendMessageToState(SendPhysicalMessageContext context)
        {
            if (!context.TryGet(out sagaUpdatedMessage))
            {
                return;
            }
            var messages = context.LogicalMessages.ToList();
            if (messages.Count > 1)
            {
                throw new Exception("The SagaAuditing plugin does not support batch messages.");
            }
            if (messages.Count == 0)
            {
                //this can happen on control messages
                return;
            }
            var logicalMessage = messages.First();

            var sagaResultingMessage = new SagaChangeOutput
                {
                    ResultingMessageId = context.MessageToSend.Id,
                    TimeSent = context.MessageToSend.Headers[Headers.TimeSent],
                    MessageType = logicalMessage.MessageType.ToString(),
                    RequestedTimeout = context.SendOptions.DelayDeliveryWith,
                    DeliveryDelay = context.SendOptions.DeliverAt,
                    Destination = context.SendOptions.Destination.ToString()
                };
            sagaUpdatedMessage.ResultingMessages.Add(sagaResultingMessage);
        }
    }
}