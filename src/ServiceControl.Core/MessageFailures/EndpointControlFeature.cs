namespace ServiceControl.EndpointControl
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.MessageFailures;

    public class MessageFailuresFeature : Feature
    {
        public override bool IsEnabledByDefault
        {
            get { return true; }
        }

        public override void Initialize()
        {
            Configure.Component<FailedMessageImporter>(DependencyLifecycle.SingleInstance);
            Configure.Component<SuccessfulRetryDetector>(DependencyLifecycle.SingleInstance);
            Configure.Component<ProcessingAttemptMessageFailureHistoryEnricher>(DependencyLifecycle.SingleInstance);
        }
    }
}