namespace ServiceControl.Audit.Persistence.InMemory
{
    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public IPersistence Create(PersistenceSettings settings) => new InMemoryPersistence(settings);
    }
}
