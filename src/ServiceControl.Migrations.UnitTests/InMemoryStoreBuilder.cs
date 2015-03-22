namespace ServiceControl.Migrations.UnitTests
{
    using System.ComponentModel.Composition.Hosting;
    using System.IO;
    using Particular.Backend.Debugging.RavenDB.Expiration;
    using Raven.Client.Embedded;
    using ServiceBus.Management.Infrastructure.Settings;

    public class InMemoryStoreBuilder
    {
        public static EmbeddableDocumentStore GetInMemoryStore(bool withExpiration = false)
        {
            var store = new EmbeddableDocumentStore
            {
                Configuration =
                {
                    RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                    RunInMemory = true
                },
                Conventions =
                {
                    SaveEnumsAsIntegers = true
                }
            };
            store.Configuration.CompiledIndexCacheDirectory = Path.GetTempPath(); // RavenDB-2236

            if (withExpiration)
            {
                Settings.ExpirationProcessTimerInSeconds = 1; // so we don't have to wait too much in tests
                store.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(ExpiredDocumentsCleanerTask).Assembly));
                store.Configuration.Settings.Add("Raven/ActiveBundles", "CustomDocumentExpiration"); // Enable the expiration bundle
            }

            store.Initialize();

            if (withExpiration)
            {
                new ExpiryProcessedMessageIndex().Execute(store); // this index is being queried by our expiration bundle
            }

            return store;
        }
    }
}