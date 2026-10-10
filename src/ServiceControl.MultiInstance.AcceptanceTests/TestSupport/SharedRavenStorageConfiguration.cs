namespace ServiceControl.MultiInstance.AcceptanceTests.TestSupport
{
    using System;
    using System.Collections.Generic;
    using System.Reactive.Disposables;
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.Persistence.RavenDB;
    using ServiceControl.Persistence.Tests;
    using ServiceControl.RavenDB;
    using TestHelper;
    using AuditRavenPersistenceConfiguration = ServiceControl.Audit.Persistence.RavenDB.RavenPersistenceConfiguration;
    using AuditStorageConfiguration = ServiceControl.Audit.AcceptanceTests.TestSupport.IAcceptanceTestStorageConfiguration;
    using PrimaryInstanceSettings = ServiceBus.Management.Infrastructure.Settings.Settings;
    using PrimaryStorageConfiguration = ServiceControl.AcceptanceTests.TestSupport.IAcceptanceTestStorageConfiguration;

    // RavenDB runs one embedded server per process, so the primary and the audit instance keep their databases on the same one
    class SharedRavenStorageConfiguration : PrimaryStorageConfiguration, AuditStorageConfiguration
    {
        // Both persisters ship a manifest named RavenDB, and only one of them survives the copy into this project's output folder
        string PrimaryStorageConfiguration.PersistenceType => TypeNameOf(typeof(RavenPersistenceConfiguration));

        string AuditStorageConfiguration.PersistenceType => TypeNameOf(typeof(AuditRavenPersistenceConfiguration));

        async Task PrimaryStorageConfiguration.CustomizeSettings(PrimaryInstanceSettings settings, CancellationToken cancellationToken)
        {
            var databaseName = Guid.NewGuid().ToString("n");
            databaseInstance = await SharedEmbeddedServer.GetInstance(cancellationToken);
            databaseNames.Add(databaseName);
            databaseNames.Add($"{databaseName}-throughput");

            settings.PersisterSpecificSettings = new RavenPersisterSettings
            {
                ErrorRetentionPeriod = TimeSpan.FromDays(10),
                ConnectionString = databaseInstance.ServerUrl,
                DatabaseName = databaseName,
                ThroughputDatabaseName = $"{databaseName}-throughput"
            };
        }

        async Task<IDictionary<string, string>> AuditStorageConfiguration.CustomizeSettings(CancellationToken cancellationToken)
        {
            var databaseName = Guid.NewGuid().ToString("n");
            databaseInstance = await SharedEmbeddedServer.GetInstance(cancellationToken);
            databaseNames.Add(databaseName);

            return new Dictionary<string, string>
            {
                { AuditRavenPersistenceConfiguration.ConnectionStringKey, databaseInstance.ServerUrl },
                { AuditRavenPersistenceConfiguration.DatabaseNameKey, databaseName }
            };
        }

        public async Task Cleanup(CancellationToken cancellationToken = default)
        {
            if (databaseInstance == null)
            {
                return;
            }
            using var _ = await UseDatabaseLifecycleLock(cancellationToken);
            foreach (var databaseName in databaseNames)
            {
                await databaseInstance.DeleteDatabase(databaseName, cancellationToken);
            }
        }

        public async Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default)
        {
            await databaseLifecycleLock.WaitAsync(cancellationToken);
            return Disposable.Create(() => databaseLifecycleLock.Release());
        }

        static string TypeNameOf(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

        static readonly SemaphoreSlim databaseLifecycleLock = new SemaphoreSlim(1, 1);
        readonly List<string> databaseNames = [];
        EmbeddedDatabase databaseInstance;
    }
}
