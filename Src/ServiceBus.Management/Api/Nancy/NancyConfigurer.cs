namespace ServiceBus.Management.Api
{
    using System;
    using NServiceBus;
    using Nancy.Hosting.Self;

    public class NancyConfigurer : INeedInitialization
    {
        public void Init()
        {
            var nancyHost = new NancyHost(new Uri("http://localhost:8888/"));

            Configure.Instance.Configurer.RegisterSingleton<NancyHost>(nancyHost);

        }
    }
}