namespace ServiceControl.Operations
{
    using Contracts.Operations;
    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;
    using NServiceBus.Satellites;
    using NServiceBus.Transports;
    using NServiceBus.Unicast.Messages;
    using ServiceBus.Management.Infrastructure.Settings;

    public class ErrorQueueImport : ISatellite
    {
        public ISendMessages Forwarder { get; set; }
        //public ImportErrorHandler ImportErrorHandler { get; set; }
        public IBuilder Builder { get; set; }

#pragma warning disable 618
        public PipelineExecutor PipelineExecutor { get; set; }
        public LogicalMessageFactory LogicalMessageFactory { get; set; }
#pragma warning restore 618

        public bool Handle(TransportMessage message)
        {
            //try
            //{
                InnerHandle(message);
            //}
            //catch (Exception exception)
            //{
            //    Logger.Error("Failed to import", exception);
            //    ImportErrorHandler.HandleError(message, exception);
            //}

            return true;
        }

        void InnerHandle(TransportMessage message)
        {
            var errorMessageReceived = new ImportFailedMessage(message);

            var logicalMessage = LogicalMessageFactory.Create(typeof(ImportFailedMessage), errorMessageReceived);

            using (var childBuilder = Builder.CreateChildBuilder())
            {
                PipelineExecutor.CurrentContext.Set(childBuilder);

                foreach (var enricher in childBuilder.BuildAll<IEnrichImportedMessages>())
                {
                    enricher.Enrich(errorMessageReceived);
                }

                PipelineExecutor.InvokeLogicalMessagePipeline(logicalMessage);
            }

            Forwarder.Send(message, Settings.ErrorLogQueue);
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

        static ILog Logger = LogManager.GetLogger(typeof(ErrorQueueImport));
    }
}