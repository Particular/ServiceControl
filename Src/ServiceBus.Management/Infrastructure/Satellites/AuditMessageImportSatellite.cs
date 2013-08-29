namespace ServiceBus.Management.Infrastructure.Satellites
{
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using NServiceBus.Unicast.Transport;
    using ServiceControl.Infrastructure.Messages;
    using Settings;

    public class AuditMessageImportSatellite : ISatellite
    {
        public IBus Bus { get; set; }

        public bool Handle(TransportMessage message)
        {
            var messageDetails = new TransportMessageDetails()
            {
                Body = message.Body,
                CorrelationId = message.CorrelationId,
                Headers = message.Headers,
                Id = message.Id,
                MessageIntent = message.MessageIntent,
                IsControlMessage = message.IsControlMessage(),
                Recoverable = message.Recoverable,
                ReplyToAddress = message.ReplyToAddress.ToString(),
                TimeSent = DateTimeExtensions.ToUtcDateTime(message.Headers[NServiceBus.Headers.TimeSent])
            };
            Bus.InMemory.Raise<AuditMessageReceived>(m => { m.MessageDetails = messageDetails; });
            return true;
        }

        public void Start()
        {
            Logger.InfoFormat("Audit import is now started, feeding audit messages from: {0}", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.AuditQueue; }
        }

        public bool Disabled
        {
            get { return InputAddress == Address.Undefined; }
        }

      

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditMessageImportSatellite));
    }
}