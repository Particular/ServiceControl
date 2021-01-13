namespace ServiceControl.Infrastructure.RavenDB
{
    using Raven.Client.Embedded;

    class EmbeddableDocumentStoreHolder
    {
        public EmbeddableDocumentStoreHolder(EmbeddableDocumentStore documentStore)
        {
            DocumentStore = documentStore;
        }
        public EmbeddableDocumentStore DocumentStore { get; }
    }
}