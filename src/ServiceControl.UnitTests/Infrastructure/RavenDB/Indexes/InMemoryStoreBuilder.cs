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
                    }
            };

        store.Initialize();
        return store;
    }
}