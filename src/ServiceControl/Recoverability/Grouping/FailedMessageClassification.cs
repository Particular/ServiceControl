﻿namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.MessageFailures.InternalMessages;

    public class FailedMessageClassification : Feature
    {
        public FailedMessageClassification()
        {
            EnableByDefault();
            RegisterStartupTask<ReclassifyErrorsAtStartup>();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Container.ConfigureComponent<ExceptionTypeAndStackTraceFailureClassifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<MessageTypeFailureClassifier>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ClassifyFailedMessageEnricher>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<AddressOfFailingEndpointClassifier>(DependencyLifecycle.SingleInstance);
        }

        class ReclassifyErrorsAtStartup : FeatureStartupTask
        {
            readonly IBus bus;

            public ReclassifyErrorsAtStartup(IBus bus)
            {
                this.bus = bus;
            }

            protected override void OnStart()
            {
                bus.SendLocal(new ReclassifyErrors());
            }
        }
    }
}
