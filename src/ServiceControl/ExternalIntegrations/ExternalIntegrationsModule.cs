namespace ServiceControl.ExternalIntegrations
{
    using Autofac;

    public class ExternalIntegrationsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            builder.RegisterType<MessageFailedPublisher>().AsImplementedInterfaces();
            builder.RegisterType<HeartbeatStoppedPublisher>().AsImplementedInterfaces();
        }
    }
}