namespace ServiceControl.Recoverability.Retries
{
    using NServiceBus;
    using NServiceBus.Transports;

    class SendBackRelocator : Relocator
    {
        readonly ISendMessages sender;

        public SendBackRelocator(ISendMessages sender)
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