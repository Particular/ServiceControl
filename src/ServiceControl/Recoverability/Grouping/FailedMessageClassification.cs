namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    class FailedMessageClassification : Feature
    {
        public override bool IsEnabledByDefault { get { return true; } }

        public FailedMessageClassification()
        {
            Configure.Component<ExceptionTypeAndStackTraceMessageGrouper>(DependencyLifecycle.SingleInstance);
            Configure.Component<ClassifyFailedMessageEnricher>(DependencyLifecycle.SingleInstance);
        }
    }

    // TODO: New Group Detection
}
