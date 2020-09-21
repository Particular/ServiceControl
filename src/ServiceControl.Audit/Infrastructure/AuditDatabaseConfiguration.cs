namespace ServiceControl.Audit.Infrastructure
{
    using System.Collections.Generic;
    using System.Reflection;
    using ServiceControl.Infrastructure.RavenDB;
    using ServiceControl.SagaAudit;

    class AuditDatabaseConfiguration : DatabaseConfiguration
    {
        public AuditDatabaseConfiguration() : base("audit") { }

        public override IEnumerable<Assembly> IndexAssemblies { get; } = new[]
        {
            typeof(AuditDatabaseConfiguration).Assembly,
            typeof(SagaInfo).Assembly
        };

        public override IEnumerable<string> CollectionsToCompress { get; } = new[]
        {
            "ProcessedMessages"
        };
    }
}