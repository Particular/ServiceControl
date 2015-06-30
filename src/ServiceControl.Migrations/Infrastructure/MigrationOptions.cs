namespace ServiceControl.Migrations
{
    using System.Collections.Generic;
    using System.Reflection;

    public class MigrationOptions
    {
        public MigrationOptions()
        {
            Assemblies = new List<Assembly>();
            MigrationResolver = new DefaultMigrationResolver();
        }

        public IList<Assembly> Assemblies { get; set; }
        public IMigrationResolver MigrationResolver { get; set; }
    }
}