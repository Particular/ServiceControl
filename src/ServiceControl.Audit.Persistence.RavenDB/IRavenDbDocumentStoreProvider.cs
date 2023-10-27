namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Raven.Client.Documents;

    interface IRavenDbDocumentStoreProvider
    {
        IDocumentStore GetDocumentStore();
    }
}