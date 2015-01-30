namespace ServiceControl.EndpointControl
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.Operations;

    public class EndpointControlFeature : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<EndpointDetectingMessageProcessor>(DependencyLifecycle.SingleInstance);
            Configure.Component<LicenseStatusMessageProcessor>(DependencyLifecycle.SingleInstance);
            Configure.Component<LicenseStatusChecker>(DependencyLifecycle.SingleInstance);
        }
    }
}