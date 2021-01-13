namespace ServiceControl.Infrastructure.RavenDB
{
    using Raven.Client.Embedded;

    internal sealed class EmbeddableDocumentStoreHolder
    {
        public EmbeddableDocumentStoreHolder(EmbeddableDocumentStore documentStore)
        {
            DocumentStore = documentStore;
        }

        public EmbeddableDocumentStore DocumentStore { get; }
    }
}