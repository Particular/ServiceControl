namespace ServiceControl.Operations
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    public interface IForwardMessages
    {
        Task Forward(MessageContext messageContext, string forwardingAddress);
    }

    public class MessageForwarder : IForwardMessages
    {
        public MessageForwarder(IDispatchMessages messageDispatcher)
        {
            this.messageDispatcher = messageDispatcher;
        }

        public Task Forward(MessageContext messageContext, string forwardingAddress)
        {
            var outgoingMessage = new OutgoingMessage(
                messageContext.MessageId,
                messageContext.Headers,
                messageContext.Body);

            if (outgoingMessage.Headers.ContainsKey(Headers.TimeToBeReceived))
            {
                outgoingMessage.Headers[Headers.TimeToBeReceived] = TimeSpan.MaxValue.ToString();
            }

            var transportOperations = new TransportOperations(
                new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress))
            );

            return messageDispatcher.Dispatch(
                transportOperations,
                messageContext.TransportTransaction,
                messageContext.Extensions
            );
        }

        IDispatchMessages messageDispatcher;
    }
}