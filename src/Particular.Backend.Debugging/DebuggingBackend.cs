namespace Particular.Backend.Debugging
{
    using NServiceBus;
    using NServiceBus.Features;
    using Particular.Backend.Debugging.Enrichers;

    public class DebuggingBackend : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<SuccessfulMessageImporter>(DependencyLifecycle.InstancePerCall);
            Configure.Component<SnapshotUpdater>(DependencyLifecycle.SingleInstance);
            Configure.Component<SagaRelationshipsEnricher>(DependencyLifecycle.SingleInstance);
            Configure.Component<TrackingIdsEnricher>(DependencyLifecycle.SingleInstance);
            Configure.Component<ProcessingStatisticsEnricher>(DependencyLifecycle.SingleInstance);
            Configure.Component<BodyEnricher>(DependencyLifecycle.SingleInstance);
            Configure.Component<MessageTypeEnricher>(DependencyLifecycle.SingleInstance);
            Configure.Component<EndpointInformationEnricher>(DependencyLifecycle.SingleInstance);
        }
    }
}