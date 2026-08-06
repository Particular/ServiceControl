namespace ServiceControl.Audit.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Nito.Disposables;
    using ServiceControl.Audit.Persistence.RavenDB;
    using ServiceControl.Audit.Persistence.Tests;
    using ServiceControl.RavenDB;

    public class AcceptanceTestStorageConfiguration
    {
        public string PersistenceType { get; } = "RavenDB";

        EmbeddedDatabase databaseInstance;
        string databaseName;
        static readonly SemaphoreSlim databaseLifecycleLock = new SemaphoreSlim(1, 1);

        public async Task<IDictionary<string, string>> CustomizeSettings()
        {
            databaseName = Guid.NewGuid().ToString("n");
            databaseInstance = await SharedEmbeddedServer.GetInstance();

            return new Dictionary<string, string>
            {
                { RavenPersistenceConfiguration.ConnectionStringKey,databaseInstance.ServerUrl },
                { RavenPersistenceConfiguration.DatabaseNameKey,databaseName}
            };
        }

        public async Task Cleanup()
        {
            if (databaseInstance == null)
            {
                return;
            }
            using var _ = await UseDatabaseLifecycleLock();
            await databaseInstance.DeleteDatabase(databaseName);
        }

        /// <summary>
        /// The shared server cannot perform database lifecycle operations in parallel, take this lock when you
        /// need to do one of these operations in a test.
        /// </summary>
        public static async Task<IDisposable> UseDatabaseLifecycleLock(CancellationToken cancellationToken = default)
        {
            await databaseLifecycleLock.WaitAsync(cancellationToken);
            return Disposable.Create(() => databaseLifecycleLock.Release());
        }
    }
}
