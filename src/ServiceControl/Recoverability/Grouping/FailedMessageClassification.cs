namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;

    public class FailedMessageClassification : Feature
    {
        public FailedMessageClassification()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ExceptionTypeAndStackTraceFailureClassifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<MessageTypeFailureClassifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ClassifyFailedMessageEnricher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<AddressOfFailingEndpointClassifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<EndpointInstanceClassifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<EndpointNameClassifier>(DependencyLifecycle.SingleInstance);
        }
    }
}