namespace ServiceControl.Infrastructure.RavenDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Sparrow.Json;

    public abstract class DatabaseConfiguration
    {
        protected DatabaseConfiguration(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"{nameof(name)} is required", nameof(name));
            Name = name;
        }

        public string Name { get; }
        public virtual IEnumerable<Assembly> IndexAssemblies => Enumerable.Empty<Assembly>();
        public virtual IEnumerable<string> CollectionsToCompress => Enumerable.Empty<string>();
        public bool EnableDocumentCompression => CollectionsToCompress.Any();
        public virtual Func<string, BlittableJsonReaderObject, string> FindClrType { get; }
    }
}