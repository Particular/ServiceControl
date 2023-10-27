namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Raven.Client.Documents;

    interface IRavenDocumentStoreProvider
    {
        IDocumentStore GetDocumentStore();
    }
}