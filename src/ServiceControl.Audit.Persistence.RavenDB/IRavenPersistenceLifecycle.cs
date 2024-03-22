namespace ServiceControl.Audit.Persistence.RavenDB
{
    interface IRavenPersistenceLifecycle : IPersistenceLifecycle, IRavenDocumentStoreProvider;
}