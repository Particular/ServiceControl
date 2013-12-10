namespace ServiceControl.Operations
{
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.Satellites;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditMessageImportSatellite : ISatellite
    {
        public IBus Bus { get; set; }

        public bool Handle(TransportMessage message)
        {
            Bus.InMemory.Raise<ImportSuccessfullyProcessedMessage>(m =>
            {
                m.UniqueMessageId = message.UniqueId();
                m.PhysicalMessage = new PhysicalMessage(message);
                m.ReceivingEndpoint = EndpointDetails.ReceivingEndpoint(message.Headers);
                m.SendingEndpoint = EndpointDetails.SendingEndpoint(message.Headers);
            });
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