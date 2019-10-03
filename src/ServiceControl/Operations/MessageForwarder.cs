namespace ServiceControl.Operations
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Extensibility;
    using NServiceBus.Routing;
    using NServiceBus.Transport;

    interface IForwardMessages
    {
        Task Forward(MessageContext messageContext, string forwardingAddress);
        Task VerifyCanReachForwardingAddress(string forwardingAddress);
    }

    class MessageForwarder : IForwardMessages
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

            // Forwarded messages should last as long as possible
            outgoingMessage.Headers.Remove(Headers.TimeToBeReceived);

            var transportOperations = new TransportOperations(
                new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardingAddress))
            );

            return messageDispatcher.Dispatch(
                transportOperations,
                messageContext.TransportTransaction,
                messageContext.Extensions
            );
        }

        public async Task VerifyCanReachForwardingAddress(string forwardingAddress)
        {
            try
            {
                var transportOperations = new TransportOperations(
                    new TransportOperation(
                        new OutgoingMessage(Guid.Empty.ToString("N"),
                            new Dictionary<string, string>(),
                            new byte[0]),
                        new UnicastAddressTag(forwardingAddress)
                    )
                );

                await messageDispatcher.Dispatch(transportOperations, new TransportTransaction(), new ContextBag())
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                throw new Exception($"Unable to write to forwarding queue {forwardingAddress}", e);
            }
        }

        IDispatchMessages messageDispatcher;
    }
}