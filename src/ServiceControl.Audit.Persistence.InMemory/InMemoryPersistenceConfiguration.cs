namespace ServiceControl.Audit.Persistence.InMemory
{
    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "InMemory";

        public IPersistence Create(PersistenceSettings settings) => new InMemoryPersistence(settings);
    }
}
