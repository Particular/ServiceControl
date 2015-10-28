using System.IO;
using Raven.Client.Embedded;

public class InMemoryStoreBuilder
{
    public static EmbeddableDocumentStore GetInMemoryStore()
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
        store.Initialize();
        return store;
    }
}