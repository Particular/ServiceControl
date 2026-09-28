namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

static class FullTextSearchSql
{
    const string CatalogName = "ServiceControlFullTextCatalog";
    const string TableName = "AuditMessages";

    const string KeyIndexName = "IX_AuditMessages_Id";

    public const string RequireFullTextSearch = """
        IF SERVERPROPERTY('IsFullTextInstalled') <> 1
        BEGIN
            THROW 50000, 'ServiceControl requires the SQL Server Full-Text Search feature, which is not installed on this instance. Install it and run setup again.', 1;
        END
        """;

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

    public static MigrationOperation Rewrite(SqlOperation operation, string schema) =>
        operation.Sql switch
        {
            var sql when sql == CreateIndex => WithSql(operation, CreateIndexSql(schema)),
            var sql when sql == DropIndex => WithSql(operation, DropIndexSql(schema)),
            _ => operation
        };

    public static bool IsHandled(string sql) =>
        sql == RequireFullTextSearch || sql == CreateCatalog || sql == DropCatalog || sql == CreateIndex || sql == DropIndex;

    static string CreateIndexSql(string? schema) => $"""
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('{Qualify(schema)}'))
        BEGIN
            EXEC('CREATE FULLTEXT INDEX ON {Qualify(schema)}(HeadersJson LANGUAGE 0, BodyText LANGUAGE 0)
                      KEY INDEX {KeyIndexName}
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
