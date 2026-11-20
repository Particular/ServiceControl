namespace ServiceControl.Persistence.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using EFCore.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

[TestFixture]
class TestSchemaTests
{
    [SetUp]
    public async Task SetUp()
    {
        connectionString = await SqlServerSharedContainer.GetConnectionStringAsync();
        await Execute($"CREATE FULLTEXT CATALOG [{catalog}]");
    }

    [TearDown]
    public async Task TearDown()
    {
        await TestSchema.Drop(connectionString, schema, skipCleanup: false);
        await TestSchema.Drop(connectionString, otherSchema, skipCleanup: false);
        await Execute($"DROP FULLTEXT CATALOG [{catalog}]");
    }

    [TestCase("")]
    [TestCase("]'quoted")]
    public async Task Retained_schema_cleanup_disables_only_its_full_text_indexes(string schemaSuffix)
    {
        schema += schemaSuffix;
        await CreateIndexedSchema(schema);
        await CreateIndexedSchema(otherSchema);
        Assert.That(await ReadSchemaState(schema), Is.EqualTo((1, 2, 2, 2)));

        await TestSchema.Drop(connectionString, schema, skipCleanup: true);
        await TestSchema.Drop(connectionString, schema, skipCleanup: true);

        Assert.That(await ReadSchemaState(schema), Is.EqualTo((1, 2, 2, 0)));
        Assert.That(await ReadSchemaState(otherSchema), Is.EqualTo((1, 2, 2, 2)));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT Body FROM {Quote(schema)}.Messages WHERE Id = 1";
        Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo("keep this row"));
    }

    [Test]
    public async Task Normal_cleanup_drops_only_its_schema_and_full_text_indexes()
    {
        await CreateIndexedSchema(schema);
        await CreateIndexedSchema(otherSchema);

        await TestSchema.Drop(connectionString, schema, skipCleanup: false);

        Assert.That(await ReadSchemaState(schema), Is.EqualTo((0, 0, 0, 0)));
        Assert.That(await ReadSchemaState(otherSchema), Is.EqualTo((1, 2, 2, 2)));
    }

    [Test]
    public async Task Retained_schema_cleanup_handles_missing_indexes_and_schemas()
    {
        await TestSchema.Create(connectionString, schema);
        await TestSchema.CreateTable(connectionString, schema, "Messages");

        await TestSchema.Drop(connectionString, schema, skipCleanup: true);
        await TestSchema.Drop(connectionString, schema, skipCleanup: true);
        await TestSchema.Drop(connectionString, otherSchema, skipCleanup: true);

        Assert.That(await ReadSchemaState(schema), Is.EqualTo((1, 1, 0, 0)));
        Assert.That(await ReadSchemaState(otherSchema), Is.EqualTo((0, 0, 0, 0)));
    }

    [Test]
    public async Task Primary_persistence_teardown_disables_retained_indexes_after_host_shutdown()
    {
        var enabledAtShutdown = -1;
        string persistenceSchema = null;
        var fixture = new PersistenceFixture(async _ => enabledAtShutdown = (await ReadSchemaState(persistenceSchema)).EnabledIndexes);
        await fixture.SetUp();
        persistenceSchema = fixture.Settings.Schema;

        await fixture.TearDown();

        Assert.That(enabledAtShutdown, Is.EqualTo(TestSchema.SkipCleanup ? 1 : 0));
        var state = await ReadSchemaState(persistenceSchema);
        Assert.That(state.Schemas, Is.EqualTo(TestSchema.SkipCleanup ? 1 : 0));
        Assert.That(state.Indexes, Is.EqualTo(TestSchema.SkipCleanup ? 1 : 0));
        Assert.That(state.EnabledIndexes, Is.Zero);
    }

    sealed class PersistenceFixture : PersistenceTestBase
    {
        public PersistenceFixture(Func<CancellationToken, Task> onStop) =>
            RegisterServices = services => services.AddSingleton<IHostedService>(new ShutdownProbe(onStop));

        public SqlServerPersisterSettings Settings => (SqlServerPersisterSettings)PersistenceSettings;
    }

    sealed class ShutdownProbe(Func<CancellationToken, Task> onStop) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => onStop(cancellationToken);
    }

    async Task CreateIndexedSchema(string schemaName)
    {
        await TestSchema.Create(connectionString, schemaName);
        foreach (var table in new[] { "Messages", "OtherMessages" })
        {
            await Execute($"""
                CREATE TABLE {Quote(schemaName)}.[{table}] (
                    Id int NOT NULL CONSTRAINT [PK_{table}] PRIMARY KEY,
                    Body nvarchar(max) NOT NULL);
                CREATE FULLTEXT INDEX ON {Quote(schemaName)}.[{table}] (Body LANGUAGE 0)
                    KEY INDEX [PK_{table}] ON [{catalog}] WITH CHANGE_TRACKING AUTO;
                INSERT INTO {Quote(schemaName)}.[{table}] (Id, Body) VALUES (1, N'keep this row');
                """);
        }
    }

    async Task<(int Schemas, int Tables, int Indexes, int EnabledIndexes)> ReadSchemaState(string schemaName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;
            SELECT (SELECT COUNT(*) FROM sys.schemas WHERE name = @schema),
                   (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(@schema)),
                   (SELECT COUNT(*) FROM sys.fulltext_indexes fi
                    JOIN sys.tables t ON fi.object_id = t.object_id WHERE t.schema_id = SCHEMA_ID(@schema)),
                   (SELECT COUNT(*) FROM sys.fulltext_indexes fi
                    JOIN sys.tables t ON fi.object_id = t.object_id WHERE t.schema_id = SCHEMA_ID(@schema) AND fi.is_enabled = 1)
            """;
        command.Parameters.AddWithValue("@schema", schemaName);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3));
    }

    async Task Execute(string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    static string Quote(string identifier) => $"[{identifier.Replace("]", "]]")}]";

    string connectionString;
    string schema = $"sc_cleanup_{Guid.NewGuid():N}";
    readonly string otherSchema = $"sc_cleanup_{Guid.NewGuid():N}";
    readonly string catalog = $"sc_cleanup_{Guid.NewGuid():N}";
}
