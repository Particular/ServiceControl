namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Transports;
    using NServiceBus.Unicast;

    class ReturnToSenderDequeuer : AdvancedDequeuer
    {
        readonly ISendMessages sender;

        public ReturnToSenderDequeuer(ISendMessages sender, Configure configure) : base(configure)
        {
            this.sender = sender;
        }

        protected override void HandleMessage(TransportMessage message)
        {
            var destinationAddress = Address.Parse(message.Headers["ServiceControl.TargetEndpointAddress"]);

            message.Headers.Remove("ServiceControl.TargetEndpointAddress");
            message.Headers.Remove("ServiceControl.Retry.StagingId");

            sender.Send(message, new SendOptions(destinationAddress));
        }
    }
}