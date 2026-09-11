namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.DataMigration;

    class MigrationSourceReportCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            await using var source = await PersistenceFactory.OpenMigrationSource(settings, cancellationToken);

            var description = await source.Describe(cancellationToken);

            Console.Out.WriteLine("ServiceControl migration source report");
            Console.Out.WriteLine();
            Console.Out.WriteLine($"Source persistence  : {settings.MigrationSourcePersistenceType} ({(description.Embedded ? "embedded" : "external")})");
            Console.Out.WriteLine($"Server URL          : {description.ServerUrl}");
            Console.Out.WriteLine($"Server version      : {description.ServerVersion}");
            Console.Out.WriteLine($"Primary database    : {description.PrimaryDatabase}  (from ServiceControl/RavenDB/DatabaseName)");
            Console.Out.WriteLine($"Throughput database : {description.ThroughputDatabase}  (from LicensingComponent/RavenDB/ThroughputDatabaseName)");

            await PrintCollections(source, MigrationSourceDatabase.Primary, description.PrimaryDatabase, cancellationToken);
            await PrintCollections(source, MigrationSourceDatabase.Throughput, description.ThroughputDatabase, cancellationToken);
        }

        static async Task PrintCollections(IMigrationSource source, MigrationSourceDatabase database, string databaseName, CancellationToken cancellationToken)
        {
            var counted = new SortedDictionary<string, long>(StringComparer.Ordinal);

            foreach (var collection in await source.CountCollections(database, cancellationToken))
            {
                counted[collection.Key] = collection.Value;
            }

            Console.Out.WriteLine();
            Console.Out.WriteLine($"Collections in {databaseName}:");

            if (counted.Count == 0)
            {
                Console.Out.WriteLine("  (none)");
                return;
            }

            foreach (var collection in counted)
            {
                Console.Out.WriteLine($"  {collection.Key,-42}{collection.Value,12:N0}");
            }
        }
    }
}
