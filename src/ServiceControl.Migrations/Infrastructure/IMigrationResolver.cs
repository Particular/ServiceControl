namespace ServiceControl.Migrations
{
    using System;

    public interface IMigrationResolver
    {
        Migration Resolve(Type migrationType);
    }
}