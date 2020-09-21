namespace Particular.ServiceControl
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using global::ServiceControl.Infrastructure.RavenDB;
    using global::ServiceControl.Infrastructure.RavenDB.Subscriptions;
    using global::ServiceControl.SagaAudit;
    using Sparrow.Json;

    class PrimaryInstanceDatabaseConfiguration : DatabaseConfiguration
    {
        public PrimaryInstanceDatabaseConfiguration() : base("servicecontrol") { }

        public override IEnumerable<Assembly> IndexAssemblies { get; } = new[]
        {
            typeof(PrimaryInstanceDatabaseConfiguration).Assembly,
            typeof(SagaInfo).Assembly
        };

        public override Func<string, BlittableJsonReaderObject, string> FindClrType { get; } =
            LegacyDocumentConversion.ConventionsFindClrType;
    }
}