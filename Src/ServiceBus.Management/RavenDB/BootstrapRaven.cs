namespace ServiceBus.Management.RavenDB
{
    using NServiceBus;
    using Raven.Client;
    using Raven.Client.Embedded;

    public class BootstrapRaven : INeedInitialization
    {
        public void Init()
        {
            var documentStore = new EmbeddableDocumentStore
                {
                    DataDirectory = "Data",
                    UseEmbeddedHttpServer = true,
                    DefaultDatabase = Configure.EndpointName
                };


            documentStore.Initialize();

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);

            Configure.Instance.RavenPersistence(() => @"Url=http://localhost:8888/Storage",
                Configure.EndpointName);
        }
    }
}