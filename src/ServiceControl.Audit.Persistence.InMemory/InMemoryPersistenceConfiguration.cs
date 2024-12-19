namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Collections.Generic;

    public class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "InMemory";

        public IEnumerable<string> ConfigurationKeys => new string[0];

        public IPersistence Create(PersistenceSettings settings) => new InMemoryPersistence(settings);
    }
}