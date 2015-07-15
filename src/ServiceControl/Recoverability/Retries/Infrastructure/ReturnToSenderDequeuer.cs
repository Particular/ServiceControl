namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Transports;

    class ReturnToSenderDequeuer : AdvancedDequeuer
    {
        readonly ISendMessages sender;

        public ReturnToSenderDequeuer(ISendMessages sender)
        {
            this.sender = sender;
        }

        protected override void HandleMessage(TransportMessage message)
        {
            var destinationAddress = Address.Parse(message.Headers["ServiceControl.TargetEndpointAddress"]);

            message.Headers.Remove("ServiceControl.TargetEndpointAddress");

            sender.Send(message, destinationAddress);
        }
    }
}