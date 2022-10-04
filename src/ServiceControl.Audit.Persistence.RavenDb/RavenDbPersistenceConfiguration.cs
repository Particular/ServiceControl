namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Raven.Client.Embedded;
    using ServiceControl.Audit.Persistence.RavenDB;

    public class RavenDbPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistence Create(PersistenceSettings settings)
        {
            var documentStore = new EmbeddableDocumentStore();
            RavenBootstrapper.Configure(documentStore, settings);

            var ravenStartup = new RavenStartup();

            foreach (var indexAssembly in RavenBootstrapper.IndexAssemblies)
            {
                ravenStartup.AddIndexAssembly(indexAssembly);
            }

            return new RavenDbPersistence(settings, documentStore, ravenStartup);
        }
    }
}
