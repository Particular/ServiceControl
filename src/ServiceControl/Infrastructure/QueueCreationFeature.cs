namespace ServiceBus.Management.Infrastructure
{
    using NServiceBus.Features;
    using NServiceBus.Transport;
    using Particular.ServiceControl;

    class QueueCreationFeature : Feature
    {
        public QueueCreationFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var componentContext = context.Settings.Get<ComponentSetupContext>();
            var queueBindings = context.Settings.Get<QueueBindings>();
            componentContext.OnQueueCreationRequested(x =>
            {
                queueBindings.BindSending(x);
            });
        }
    }
}