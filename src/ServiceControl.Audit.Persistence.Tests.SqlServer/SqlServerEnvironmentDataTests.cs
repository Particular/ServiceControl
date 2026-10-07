namespace ServiceControl.Audit.Persistence.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging.Abstractions;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.Abstractions;
    using ServiceControl.Audit.Persistence.EFCore.Infrastructure;
    using ServiceControl.Audit.Persistence.EFCore.SqlServer;

    class SqlServerEnvironmentDataTests : EFPersistenceTestFixture
    {
        [Test]
        public async Task Reports_the_edition_family()
        {
            var hosting = await ServiceProvider.GetRequiredService<IDatabaseHostingProbe>().Probe(TestTimeoutCancellationToken);
            var managed = hosting.Hosting is "AzureSql" or "AzureSqlManagedInstance";

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hosting.ServerEdition, managed ? Is.EqualTo(DatabaseHosting.NotApplicable) : Is.AnyOf("Express", "Standard", "Enterprise"));
                Assert.That(hosting.ServiceObjective, hosting.Hosting == "AzureSql" ? Is.AnyOf(ServiceTiers) : Is.EqualTo(DatabaseHosting.NotApplicable));
            }
        }

        [Test]
        public async Task Counts_the_audit_messages_it_stores()
        {
            await Ingest(MakeMessage(), MakeMessage());

            var footprint = await ServiceProvider.GetRequiredService<IStorageFootprintProbe>().Probe(TestTimeoutCancellationToken);

            Assert.That(footprint, Is.Not.Null);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(footprint.MessageCount, Is.EqualTo(2));
                Assert.That(footprint.SizeGB, Is.GreaterThan(0));
            }
        }

        [TestCase(2, "Standard")]
        [TestCase(3, "Enterprise")]
        [TestCase(4, "Express")]
        [TestCase(5, "NotApplicable")]
        [TestCase(8, "NotApplicable")]
        [TestCase(1, "Other")]
        [TestCase(9, "Other")]
        [TestCase(12, "Other")]
        public void Should_report_the_edition_family(int engineEdition, string expected) =>
            Assert.That(SqlServerDatabaseHostingProbe.EditionFamily(engineEdition), Is.EqualTo(expected));

        [TestCase(null, "NotApplicable")]
        [TestCase("Basic", "Basic")]
        [TestCase("S0", "Standard")]
        [TestCase("S12", "Standard")]
        [TestCase("P15", "Premium")]
        [TestCase("GP_Gen5_2", "GeneralPurpose")]
        [TestCase("GP_S_Gen5_1", "GeneralPurpose")]
        [TestCase("GP_DC_8", "GeneralPurpose")]
        [TestCase("BC_Gen5_8", "BusinessCritical")]
        [TestCase("HS_Gen5_4", "Hyperscale")]
        [TestCase("ElasticPool", "ElasticPool")]
        [TestCase("DW100c", "Other")]
        [TestCase("System2", "Other")]
        public void Should_report_only_the_service_objective_tier(string serviceObjective, string expected) =>
            Assert.That(SqlServerDatabaseHostingProbe.ServiceObjectiveTier(serviceObjective), Is.EqualTo(expected));

        [TestCase("Server=sc.database.windows.net;Database=sc")]
        [TestCase("not a connection string")]
        public async Task Should_report_edition_and_service_objective_as_unknown_when_the_server_cannot_be_asked(string connectionString)
        {
            var settings = new EFPersisterSettings { ConnectionString = connectionString, AuditRetentionPeriod = TimeSpan.FromDays(7), MaxBodySizeToStore = 1024 };
            var probe = new SqlServerDatabaseHostingProbe(settings, new UnreachableScopeFactory(), NullLogger<SqlServerDatabaseHostingProbe>.Instance);

            var hosting = await probe.Probe(TestTimeoutCancellationToken);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(hosting.ServerEdition, Is.EqualTo("Unknown"));
                Assert.That(hosting.ServiceObjective, Is.EqualTo("Unknown"));
            }
        }

        [Test]
        public async Task Counts_the_full_text_index_in_the_size()
        {
            await Ingest(MakeMessage(), MakeMessage());

            var fullTextGB = await WithDbContext(async (dbContext, token) =>
            {
                await dbContext.Database.OpenConnectionAsync(token);
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = """
                    SELECT COALESCE(SUM(a.total_pages), 0) * 8.0 / 1048576
                    FROM sys.internal_tables it
                    JOIN sys.tables t ON t.object_id = it.parent_id
                    JOIN sys.partitions p ON p.object_id = it.object_id
                    JOIN sys.allocation_units a ON a.container_id = p.partition_id
                    WHERE t.schema_id = SCHEMA_ID(@schema) AND it.internal_type_desc LIKE 'FULLTEXT%'
                    """;
                var schema = command.CreateParameter();
                schema.ParameterName = "@schema";
                schema.Value = dbContext.Schema;
                command.Parameters.Add(schema);

                return Convert.ToDouble(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
            });

            var tablesGB = await WithDbContext(async (dbContext, token) =>
            {
                await dbContext.Database.OpenConnectionAsync(token);
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = """
                    SELECT SUM(a.total_pages) * 8.0 / 1048576
                    FROM sys.tables t
                    JOIN sys.partitions p ON p.object_id = t.object_id
                    JOIN sys.allocation_units a ON a.container_id = p.partition_id
                    WHERE t.schema_id = SCHEMA_ID(@schema)
                    """;
                var schema = command.CreateParameter();
                schema.ParameterName = "@schema";
                schema.Value = dbContext.Schema;
                command.Parameters.Add(schema);

                return Convert.ToDouble(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
            });

            var footprint = await ServiceProvider.GetRequiredService<IStorageFootprintProbe>().Probe(TestTimeoutCancellationToken);

            Assert.That(fullTextGB, Is.GreaterThan(0), "The audit messages table always has a full-text index");
            Assert.That(footprint?.SizeGB, Is.EqualTo(tablesGB + fullTextGB).Within(1e-9));
        }

        static readonly object[] ServiceTiers = ["Basic", "Standard", "Premium", "GeneralPurpose", "BusinessCritical", "Hyperscale", "ElasticPool"];

        sealed class UnreachableScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope() => throw new InvalidOperationException("The database cannot be reached");
        }
    }
}
