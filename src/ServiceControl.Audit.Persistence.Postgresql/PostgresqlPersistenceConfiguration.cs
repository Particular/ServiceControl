namespace ServiceControl.Audit.Persistence.Postgresql
{
    using System.Collections.Generic;
    using ServiceControl.Audit.Persistence;

    public class PostgresqlPersistenceConfiguration : IPersistenceConfiguration
    {
        public string Name => "PostgreSQL";

        public IEnumerable<string> ConfigurationKeys => new[] { "ConnectionString", "MaxConnections" };

        public IPersistence Create(PersistenceSettings settings) => throw new System.NotImplementedException();
    }
}
