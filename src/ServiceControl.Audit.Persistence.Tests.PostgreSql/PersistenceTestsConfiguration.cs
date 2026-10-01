namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Threading.Tasks;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;
    using ServiceControl.Persistence.Tests;

    class PersistenceTestsConfiguration : EFPersistenceTestsConfiguration
    {
        public override string Name => "PostgreSQL";

        public override Task<string> GetConnectionString() => PostgreSqlSharedContainer.GetConnectionStringAsync();

        protected override Task CreateSchema(string connectionString, string schema) => TestSchema.Create(connectionString, schema);

        protected override Task DropSchema(string connectionString, string schema) => TestSchema.Drop(connectionString, schema);

        public override EFPersistenceConfigurationBase CreateConfiguration() => new PostgreSqlPersistenceConfiguration();
    }
}
