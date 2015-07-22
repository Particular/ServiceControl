namespace ServiceControl.Migrations
{
    using System;

    public class DefaultMigrationResolver : IMigrationResolver
    {
        public Migration Resolve(Type migrationType)
        {
            return (Migration)Activator.CreateInstance(migrationType);
        }
    }
}