namespace ServiceControl.Hosting.Commands
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Particular.ServiceControl.Hosting;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Persistence;

    class MigrationSourceReportCommand : AbstractCommand
    {
        public override async Task Execute(HostArguments args, Settings settings, CancellationToken cancellationToken = default)
        {
            await using var source = await PersistenceFactory.OpenMigrationSource(settings, cancellationToken);

            var description = await source.Describe(cancellationToken);

            Console.Out.WriteLine("ServiceControl migration source report");
            Console.Out.WriteLine();
            Console.Out.WriteLine($"{"Source persistence",-20}: {settings.MigrationSourcePersistenceType}");
            Console.Out.WriteLine($"{"Version",-20}: {description.Version}");

            foreach (var fact in description.Facts)
            {
                var origin = fact.SettingKey is null ? string.Empty : $"  (from {fact.SettingKey})";
                Console.Out.WriteLine($"{fact.Label,-20}: {fact.Value}{origin}");
            }

            foreach (var scope in (await source.Inventory(cancellationToken)).GroupBy(entry => entry.Scope))
            {
                Console.Out.WriteLine();
                Console.Out.WriteLine($"{scope.Key}:");

                foreach (var entry in scope.OrderBy(entry => entry.Name, StringComparer.Ordinal))
                {
                    Console.Out.WriteLine($"  {entry.Name,-42}{entry.Count,12:N0}");
                }
            }
        }
    }
}
