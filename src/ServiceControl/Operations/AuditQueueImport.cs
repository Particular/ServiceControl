namespace ServiceControl.Operations
{
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Persistence.Raven;
    using NServiceBus.Satellites;
    using ServiceBus.Management.Infrastructure.Settings;

    public class AuditQueueImport : ISatellite
    {
        public IBus Bus { get; set; }
        public IBuilder Builder { get; set; }

        public bool Handle(TransportMessage message)
        {
            var receivedMessage = new ImportSuccessfullyProcessedMessage(message);

         
            var sessionFactory = Builder.Build<RavenSessionFactory>();

            try
            {
                foreach (var enricher in Builder.BuildAll<IEnrichImportedMessages>())
                {
                    enricher.Enrich(receivedMessage);
                }

                Bus.InMemory.Raise(receivedMessage);

                sessionFactory.SaveChanges();
            }
            finally
            {
                sessionFactory.ReleaseSession();
            }
          
            
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

      

        static readonly ILog Logger = LogManager.GetLogger(typeof(AuditQueueImport));
    }
}