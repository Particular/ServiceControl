namespace ServiceControl.Audit.Persistence.RavenDB
{
    interface IRavenDbPersistenceLifecycle : IPersistenceLifecycle, IRavenDbDocumentStoreProvider
    {
    }
}