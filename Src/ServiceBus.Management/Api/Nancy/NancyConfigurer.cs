namespace ServiceBus.Management.Api.Nancy
{
    using System;
    using NServiceBus;
    using global::Nancy.Hosting.Self;

    public class NancyConfigurer : INeedInitialization
    {
        public void Init()
        {
            //we need to use a func here to delay the nancy modules to load since we need to configure its dependencies first
            Configure.Instance.Configurer.ConfigureComponent(() => new NancyHost(new Uri("http://localhost:8888/")), DependencyLifecycle.SingleInstance);
        }
    }
}