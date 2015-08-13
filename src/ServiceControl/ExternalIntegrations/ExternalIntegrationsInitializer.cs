namespace ServiceControl.ExternalIntegrations
{
    using NServiceBus;

    public class ExternalIntegrationsInitializer : INeedInitialization
    {
        public void Customize(BusConfiguration configuration)
        {
            configuration.RegisterComponents(c =>
            {
                c.ConfigureComponent<MessageFailedPublisher>(DependencyLifecycle.SingleInstance);
                c.ConfigureComponent<HeartbeatStoppedPublisher>(DependencyLifecycle.SingleInstance);
                c.ConfigureComponent<HeartbeatRestoredPublisher>(DependencyLifecycle.SingleInstance);
                c.ConfigureComponent<CustomCheckFailedPublisher>(DependencyLifecycle.SingleInstance);
                c.ConfigureComponent<CustomCheckSucceededPublisher>(DependencyLifecycle.SingleInstance);
            });        
        }
    }
}