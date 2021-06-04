namespace ServiceControl.Infrastructure.RavenDB
{
    using System.Collections.Generic;
    using System.ComponentModel.Composition.Hosting;
    using System.Linq;
    using System.Reflection;

    class RavenStartup
    {
        readonly List<Assembly> indexAssemblies = new List<Assembly>();
        public void AddIndexAssembly(Assembly assembly) => indexAssemblies.Add(assembly);

        internal ExportProvider CreateIndexProvider() =>
            new CompositionContainer(
                new AggregateCatalog(
                    from indexAssembly in indexAssemblies select new AssemblyCatalog(indexAssembly)
                )
            );
    }
}