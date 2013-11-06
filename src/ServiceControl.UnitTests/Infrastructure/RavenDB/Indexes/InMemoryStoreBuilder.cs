using Raven.Client.Embedded;

public class InMemoryStoreBuilder
{
    public static EmbeddableDocumentStore GetInMemoryStore()
    {
        return new EmbeddableDocumentStore
        {
            Configuration =
            {
                RunInUnreliableYetFastModeThatIsNotSuitableForProduction = true,
                RunInMemory = true
            }
        };
    }
}