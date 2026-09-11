namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Full text search DDL for the failed messages table. EF Core has no full text support for SQL
/// Server, so it is applied by the AddFullTextSearch migration. The statements live here, and not
/// in the migration itself, so that regenerating the migrations with the dotnet-ef CLI only costs
/// a one line migration body.
/// </summary>
static class FullTextSearchSql
{
    const string CatalogName = "ServiceControlFullTextCatalog";
    const string TableName = "FailedMessages";

    // Message search is not optional, so an instance without Full-Text Search installed is not a
    // degraded instance, it is a broken one: every /messages/search request would fail on a missing
    // index. Failing the migration says so once, at setup, instead of at the first search.
    public const string RequireFullTextSearch = """
        IF SERVERPROPERTY('IsFullTextInstalled') <> 1
        BEGIN
            THROW 50000, 'ServiceControl requires the SQL Server Full-Text Search feature, which is not installed on this instance. Install it and run setup again.', 1;
        END
        """;

    // A catalog belongs to the database, not to a schema, so instances configured with different
    // schemas in one database share this one and can reach the CREATE together. The re-check in the
    // CATCH is what makes losing that race harmless.
    // The statements are idempotent so that a re-run is harmless. They also cannot run inside a
    // transaction, so the migration passes suppressTransaction.
    public const string CreateCatalog = $"""
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{CatalogName}')
        BEGIN
            BEGIN TRY
                EXEC('CREATE FULLTEXT CATALOG {CatalogName}');
            END TRY
            BEGIN CATCH
                IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{CatalogName}') THROW;
            END CATCH
        END
        """;

    // Only when this is the last index in the catalog, otherwise rolling back one schema's
    // migration would take full text search away from every other schema in the database.
    public const string DropCatalog = $"""
        IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{CatalogName}')
           AND NOT EXISTS (SELECT 1
                           FROM sys.fulltext_indexes i
                           JOIN sys.fulltext_catalogs c ON i.fulltext_catalog_id = c.fulltext_catalog_id
                           WHERE c.name = '{CatalogName}')
        BEGIN
            DROP FULLTEXT CATALOG {CatalogName};
        END
        """;

    public static readonly string CreateIndex = CreateIndexSql(null);

    public static readonly string DropIndex = DropIndexSql(null);

    /// <summary>
    /// Re-renders the statement the migration carries, this time with the configured schema in it.
    /// Anything else is left alone: EF Core builds the migrations history table's own SQL through
    /// the same generator, already pointed at the right schema. MigrationSqlIsSchemaAwareTests is
    /// what catches a statement of ours that should have been listed here.
    /// </summary>
    public static MigrationOperation Rewrite(SqlOperation operation, string schema) =>
        operation.Sql switch
        {
            var sql when sql == CreateIndex => WithSql(operation, CreateIndexSql(schema)),
            var sql when sql == DropIndex => WithSql(operation, DropIndexSql(schema)),
            _ => operation
        };

    // The catalog statements count as handled without being rewritten: they are server and database
    // scoped, so no schema reaches them.
    public static bool IsHandled(string sql) =>
        sql == RequireFullTextSearch || sql == CreateCatalog || sql == DropCatalog || sql == CreateIndex || sql == DropIndex;

    // LANGUAGE 0 (neutral) and STOPLIST = OFF keep the word breaker from applying language rules
    // and from dropping stopwords, both of which lose matches on technical content.
    // The message type needs no dedicated column here: the word breaker splits dotted names, and
    // the headers already carry the type.
    static string CreateIndexSql(string? schema) => $"""
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('{Qualify(schema)}'))
        BEGIN
            EXEC('CREATE FULLTEXT INDEX ON {Qualify(schema)}(HeadersJson LANGUAGE 0, BodyText LANGUAGE 0)
                      KEY INDEX PK_{TableName}
                      ON {CatalogName}
                      WITH (CHANGE_TRACKING AUTO, STOPLIST = OFF)');
        END
        """;

    static string DropIndexSql(string? schema) => $"""
        IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('{Qualify(schema)}'))
        BEGIN
            DROP FULLTEXT INDEX ON {Qualify(schema)};
        END
        """;

    static string Qualify(string? schema) => schema is null ? TableName : $"[{schema}].[{TableName}]";

    static SqlOperation WithSql(SqlOperation operation, string sql) =>
        new() { Sql = sql, SuppressTransaction = operation.SuppressTransaction };
}
