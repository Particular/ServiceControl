namespace ServiceControl.Audit.Persistence.RavenDb
{
    using Raven.Client.Documents.Session;

    interface IRavenDbSessionProvider
    {
        IAsyncDocumentSession OpenSession();
    }
}