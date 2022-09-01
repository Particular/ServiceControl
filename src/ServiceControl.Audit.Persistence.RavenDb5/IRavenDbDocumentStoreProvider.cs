namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Raven.Client.Documents;

    interface IRavenDbDocumentStoreProvider
    {
        IDocumentStore GetDocumentStore();
    }
}