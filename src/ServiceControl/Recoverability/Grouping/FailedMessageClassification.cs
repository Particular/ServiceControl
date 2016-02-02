namespace ServiceControl.Recoverability
{
    using NServiceBus;
    using NServiceBus.Features;
    using ServiceControl.MessageFailures.InternalMessages;

    class FailedMessageClassification : Feature
    {
        public FailedMessageClassification()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.RegisterStartupTask(builder => builder.Build<ReclassifyErrorsAtStartup>());
            context.Container.ConfigureComponent<ExceptionTypeAndStackTraceMessageGrouper>(DependencyLifecycle.SingleInstance);
            context.Container.ConfigureComponent<ClassifyFailedMessageEnricher>(DependencyLifecycle.SingleInstance);
        }

        class ReclassifyErrorsAtStartup : FeatureStartupTask
        {
            readonly IBusSession busSession;

            public ReclassifyErrorsAtStartup(IBusSession busSession)
            {
                this.busSession = busSession;
            }

            protected override void OnStart()
            {
                busSession.SendLocal(new ReclassifyErrors()).GetAwaiter().GetResult();
            }
        }
    }
}
