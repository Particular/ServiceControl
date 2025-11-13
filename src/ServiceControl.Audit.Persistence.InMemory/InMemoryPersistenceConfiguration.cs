namespace ServiceControl.Audit.Persistence.InMemory
{
    using System.Collections.Generic;
    using Microsoft.Extensions.Configuration;

    public sealed class InMemoryPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "InMemory";

        public IPersistence Create(PersistenceSettings settings, IConfiguration configuration) => new InMemoryPersistence(settings);
    }
}
