namespace ServiceBus.Management.RavenDB
{
    using NServiceBus;
    using Nancy.TinyIoc;
    using Raven.Client;
    using Raven.Client.Embedded;

    public class RavenBootstrapper : INeedInitialization
    {
        //for now
        public static IDocumentStore Store { get; private set; }

        public void Init()
        {
            var documentStore = new EmbeddableDocumentStore
                {
                    DataDirectory = "Data",
                    UseEmbeddedHttpServer = true
                };


            documentStore.Initialize();


            Store = documentStore;

            Configure.Instance.Configurer.RegisterSingleton<IDocumentStore>(documentStore);

            
            Configure.Instance.RavenPersistence(() => @"Url=http://localhost:8888/Storage",
                Configure.EndpointName);
        }

        IDocumentStore ResolveStore(TinyIoCContainer arg1, NamedParameterOverloads arg2)
        {
            return Configure.Instance.Builder.Build<IDocumentStore>();
        }
    }
}