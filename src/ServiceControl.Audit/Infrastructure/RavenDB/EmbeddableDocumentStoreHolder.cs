namespace ServiceControl.Audit.Infrastructure.RavenDB
{
    using Raven.Client.Embedded;

    sealed class EmbeddableDocumentStoreHolder
    {
        public EmbeddableDocumentStoreHolder(EmbeddableDocumentStore documentStore)
        {
            DocumentStore = documentStore;
        }

        public EmbeddableDocumentStore DocumentStore { get; }
    }
}