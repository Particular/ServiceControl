namespace ServiceControl.Audit.Persistence.RavenDB
{
    using Raven.Client.Documents.Session;

    interface IRavenSessionProvider
    {
        IAsyncDocumentSession OpenSession();
    }
}