namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    class FailedMessageClassification : Feature
    {
        public FailedMessageClassification()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ExceptionTypeAndStackTraceMessageGrouper>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ClassifyFailedMessageEnricher>(DependencyLifecycle.SingleInstance);
        }
    }
}
