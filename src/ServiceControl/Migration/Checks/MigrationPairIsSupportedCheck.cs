namespace ServiceControl.Migration.Checks;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Persistence;
using ServiceControl.Persistence.DataMigration;

/// <summary>
/// Refuses any pair of databases but RavenDB into SQL Server or PostgreSQL. The seams underneath are general
/// enough to copy between any two persisters, and this is the check that says which pair is actually supported.
/// </summary>
class MigrationPairIsSupportedCheck(Settings settings) : IMigrationStartupCheck
{
    public string Name => "the source and target are the supported pair";

    public Task Run(CancellationToken cancellationToken = default)
    {
        var sourceName = PersistenceManifestLibrary.Find(settings.MigrationSourcePersistenceType)?.Name;
        var targetName = PersistenceManifestLibrary.Find(settings.PersistenceType)?.Name;

        if (!string.Equals(sourceName, MigrationSettings.DefaultSourcePersistenceType, StringComparison.OrdinalIgnoreCase)
            || !PersistenceFactory.SqlPersistenceNames.Contains(targetName, StringComparer.OrdinalIgnoreCase))
        {
            throw new Exception(
                $"Migrating from '{settings.MigrationSourcePersistenceType}' to '{settings.PersistenceType}' is not supported. The only supported migration is from RavenDB to SQL Server or PostgreSQL, so set {Settings.SettingsRootNamespace}/{MigrationSettings.SourcePersistenceTypeKey} to {MigrationSettings.DefaultSourcePersistenceType} and {Settings.SettingsRootNamespace}/PersistenceType to {string.Join(" or ", PersistenceFactory.SqlPersistenceNames)} before setting {MigrationSettings.EnabledKey}.");
        }

        return Task.CompletedTask;
    }
}
