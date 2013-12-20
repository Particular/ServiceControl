namespace ServiceControl.Operations
{
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Persistence.Raven;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ErrorQueueImport : ISatellite
    {
        public IBus Bus { get; set; }
        public ISendMessages Forwarder { get; set; }

        public IBuilder Builder { get; set; }

        public bool Handle(TransportMessage message)
        {
            var errorMessageReceived = new ImportFailedMessage(message);


            var sessionFactory = Builder.Build<RavenSessionFactory>();

            try
            {
                foreach (var enricher in Builder.BuildAll<IEnrichImportedMessages>())
                {
                    enricher.Enrich(errorMessageReceived);
                }

                Bus.InMemory.Raise(errorMessageReceived);

                Forwarder.Send(message, Settings.ErrorLogQueue);

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
            Logger.InfoFormat("Error import is now started, feeding error messages from: {0}", InputAddress);
        }

        public void Stop()
        {
        }

        public Address InputAddress
        {
            get { return Settings.ErrorQueue; }
        }

        public bool Disabled
        {
            get { return InputAddress == Address.Undefined; }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));
    }
}