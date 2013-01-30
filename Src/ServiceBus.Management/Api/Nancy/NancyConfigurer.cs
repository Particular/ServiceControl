namespace ServiceBus.Management.Api.Nancy
{
    using System;
    using NServiceBus;
    using global::Nancy.Hosting.Self;

    public class NancyConfigurer : INeedInitialization
    {
        public void Init()
        {
            var nancyHost = new NancyHost(new Uri("http://localhost:8888/"));

            Configure.Instance.Configurer.RegisterSingleton<NancyHost>(nancyHost);
        }
    }
}