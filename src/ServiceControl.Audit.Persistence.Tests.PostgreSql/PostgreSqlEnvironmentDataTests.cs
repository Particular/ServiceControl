namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Npgsql;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.DbContexts;
    using ServiceControl.Audit.Persistence.EFCore.Entities;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

    class PostgreSqlEnvironmentDataTests : EFPersistenceTestFixture
    {
        [Test]
        public async Task Reports_no_edition_or_service_objective()
        {
            var hosting = await ServiceProvider.GetRequiredService<IDatabaseHostingProbe>().Probe(TestTimeoutCancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hosting.ServerEdition, Is.EqualTo(DatabaseHosting.NotApplicable));
                Assert.That(hosting.ServiceObjective, Is.EqualTo(DatabaseHosting.NotApplicable));
            }
        }

        [Test]
        public async Task Counts_messages_across_the_partitions_once_analysed()
        {
            await Ingest(MakeMessage(), MakeMessage());
            configuration.TimeProvider.Advance(TimeSpan.FromDays(1));
            await Ingest(MakeMessage());

            var parentRowEstimate = await WithDbContext(async (dbContext, token) =>
            {
                var table = $"{dbContext.Schema}.{dbContext.Model.FindEntityType(typeof(AuditMessageEntity))!.GetTableName()}";
                var leaves = await Query(dbContext, $"SELECT relid::regclass::text FROM pg_partition_tree('{table}'::regclass) WHERE isleaf", token);

                foreach (var leaf in leaves)
                {
                    await Query(dbContext, $"ANALYZE {leaf}", token);
                }

                return (await Query(dbContext, $"SELECT reltuples::text FROM pg_class WHERE oid = '{table}'::regclass", token)).Single();
            });

            var footprint = await ServiceProvider.GetRequiredService<IStorageFootprintProbe>().Probe(TestTimeoutCancellationToken);

            Assert.That(footprint, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(double.Parse(parentRowEstimate, CultureInfo.InvariantCulture), Is.LessThan(0), "The partitioned parent carries no row estimate of its own");
                Assert.That(footprint.MessageCount, Is.EqualTo(3));
                Assert.That(footprint.SizeGB, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task Measures_and_identifies_the_search_path_schema_when_none_is_configured()
        {
            var settings = new PersistenceSettings(TimeSpan.FromDays(1), true, 100000);
            settings.PersisterSpecificSettings[EFPersistenceConfigurationBase.ConnectionStringKey] =
                new NpgsqlConnectionStringBuilder(configuration.ConnectionString) { SearchPath = configuration.Schema }.ConnectionString;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(configuration.TimeProvider);
            configuration.CreateConfiguration().Create(settings).AddPersistence(services);
            await using var unconfigured = services.BuildServiceProvider();

            var identity = await unconfigured.GetRequiredService<IStorageIdentityProvider>().GetIdentity(TestTimeoutCancellationToken);
            var footprint = await unconfigured.GetRequiredService<IStorageFootprintProbe>().Probe(TestTimeoutCancellationToken);
            var expected = await ServiceProvider.GetRequiredService<IStorageFootprintProbe>().Probe(TestTimeoutCancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(identity?.Schema, Is.EqualTo(configuration.Schema));
                Assert.That(footprint?.SizeGB, Is.Not.Null.And.EqualTo(expected?.SizeGB));
            }
        }

        static async Task<List<string>> Query(AuditDbContext dbContext, string sql, CancellationToken cancellationToken)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }
    }
}
