namespace ServiceControl.Audit.Persistence.RavenDb5
{
    using Sparrow.Json;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System;

    public abstract class DatabaseConfiguration
    {
        protected DatabaseConfiguration(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"{nameof(name)} is required", nameof(name));
            }

            Name = name;
        }

        public string Name { get; }
        public virtual IEnumerable<Assembly> IndexAssemblies => Enumerable.Empty<Assembly>();
        public virtual IEnumerable<string> CollectionsToCompress => Enumerable.Empty<string>();
        public bool EnableDocumentCompression => CollectionsToCompress.Any();
        public virtual Func<string, BlittableJsonReaderObject, string> FindClrType { get; }
    }

    public class AuditDatabaseConfiguration : DatabaseConfiguration
    {
        public AuditDatabaseConfiguration(string name = null) : base(name ?? "audit")
        {
        }

        public override IEnumerable<Assembly> IndexAssemblies { get; } = new[]
        {
            typeof(AuditDatabaseConfiguration).Assembly
        };

        // public override IEnumerable<string> CollectionsToCompress { get; } = new[]
        // {
        //     "ProcessedMessages"
        // };
    }
}
