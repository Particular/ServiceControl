namespace NServiceBus
{
    using Transports;

    /// <summary>
    /// Transport definition for LearningTransport
    /// </summary>
    public class LearningTransport : TransportDefinition
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public LearningTransport()
        {
            HasNativePubSubSupport = true;
            HasSupportForCentralizedPubSub = true;
            HasSupportForDistributedTransactions = false;
        }

        /// <summary>
        /// Gives implementations access to the <see cref="BusConfiguration"/> instance at configuration time.
        /// </summary>
        /// <param name="config"></param>
        protected override void Configure(BusConfiguration config)
        {
            config.EnableFeature<LearningTransportConfigurator>();
        }
    }
}
