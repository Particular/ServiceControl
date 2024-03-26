namespace ServiceControl.Persistence.RavenDB
{
    // Currently this is a marker interface as an intermediate step. Eventually once the custom lifetimes are gone
    // it is possible to align things with the audit instance by moving the initialize method from the base interface
    // to this one and delete the base interface. One step at a time.
    interface IRavenPersistenceLifecycle : IPersistenceLifecycle;
}