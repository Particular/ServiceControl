namespace Particular.Operations.Ingestion
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageTypes;

    public class IngestionFeature : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<TransportMessageProcessor>(DependencyLifecycle.SingleInstance);
            Configure.Component<EndpointInstanceParser>(DependencyLifecycle.SingleInstance);
            Configure.Component<MessageTypeParser>(DependencyLifecycle.SingleInstance);
            Configure.Component<IdGenerator>(DependencyLifecycle.SingleInstance);
        }
    }
}
