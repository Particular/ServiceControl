namespace ServiceControl.Persistence.EFCore.SqlServer;

/// <summary>
/// Full text search DDL for the failed messages table. EF Core has no full text support for SQL
/// Server, so it is applied by the AddFullTextSearch migration. The statements live here, and not
/// in the migration itself, so that regenerating the migrations with the dotnet-ef CLI only costs
/// a one line migration body.
/// </summary>
static class FullTextSearchSql
{
    const string CatalogName = "ServiceControlFullTextCatalog";

    // Message search is not optional, so an instance without Full-Text Search installed is not a
    // degraded instance, it is a broken one: every /messages/search request would fail on a missing
    // index. Failing the migration says so once, at setup, instead of at the first search.
    public const string RequireFullTextSearch = """
        IF SERVERPROPERTY('IsFullTextInstalled') <> 1
        BEGIN
            THROW 50000, 'ServiceControl requires the SQL Server Full-Text Search feature, which is not installed on this instance. Install it and run setup again.', 1;
        END
        """;

    // The statements are idempotent so that a re-run is harmless. They also cannot run inside a
    // transaction, so the migration passes suppressTransaction.
    public const string CreateCatalog = $"""
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{CatalogName}')
        BEGIN
            EXEC('CREATE FULLTEXT CATALOG {CatalogName}');
        END
        """;

    // LANGUAGE 0 (neutral) and STOPLIST = OFF keep the word breaker from applying language rules
    // and from dropping stopwords, both of which lose matches on technical content.
    // The message type needs no dedicated column here: the word breaker splits dotted names, and
    // the headers already carry the type.
    public const string CreateIndex = $"""
        IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('FailedMessages'))
        BEGIN
            EXEC('CREATE FULLTEXT INDEX ON FailedMessages(HeadersJson LANGUAGE 0, BodyText LANGUAGE 0)
                      KEY INDEX PK_FailedMessages
                      ON {CatalogName}
                      WITH (CHANGE_TRACKING AUTO, STOPLIST = OFF)');
        END
        """;

    public const string DropIndex = """
        IF EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('FailedMessages'))
        BEGIN
            DROP FULLTEXT INDEX ON FailedMessages;
        END
        """;

    public const string DropCatalog = $"""
        IF EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{CatalogName}')
        BEGIN
            DROP FULLTEXT CATALOG {CatalogName};
        END
        """;
}
