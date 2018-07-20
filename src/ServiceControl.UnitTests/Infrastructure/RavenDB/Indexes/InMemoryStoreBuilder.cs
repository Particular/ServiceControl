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
                RunInMemory = true,
                CompiledIndexCacheDirectory = Path.GetTempPath() // RavenDB-2236
            },
            Conventions =
            {
                SaveEnumsAsIntegers = true
            }
        };
        store.Initialize();

        return store;
    }
}