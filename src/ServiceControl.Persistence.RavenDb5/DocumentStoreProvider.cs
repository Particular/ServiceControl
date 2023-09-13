namespace ServiceControl.Persistence.RavenDb5
{
    using System;
    using Raven.Client.Documents;

    class DocumentStoreProvider
    {
        readonly Func<IDocumentStore> documentStoreFactory;
        public IDocumentStore Store => documentStoreFactory();

        public DocumentStoreProvider(Func<IDocumentStore> documentStoreFactory) => this.documentStoreFactory = documentStoreFactory;
    }
}