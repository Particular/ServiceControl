namespace ServiceControl.Audit.Persistence.RavenDb
{
    interface IRavenDbPersistenceLifecycle : IPersistenceLifecycle, IRavenDbDocumentStoreProvider
    {
    }
}