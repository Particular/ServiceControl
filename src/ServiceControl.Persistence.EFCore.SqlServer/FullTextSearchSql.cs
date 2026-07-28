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

    // Both statements are guarded: an instance without Full-Text Search installed still migrates,
    // it just has no full text index. They also cannot run inside a transaction, so the migration
    // passes suppressTransaction.
    public const string CreateCatalog = $"""
        IF SERVERPROPERTY('IsFullTextInstalled') = 1
            AND NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = '{CatalogName}')
        BEGIN
            EXEC('CREATE FULLTEXT CATALOG {CatalogName}');
        END
        """;

    // LANGUAGE 0 (neutral) and STOPLIST = OFF keep the word breaker from applying language rules
    // and from dropping stopwords, both of which lose matches on technical content.
    // The message type needs no dedicated column here: the word breaker splits dotted names, and
    // the headers already carry the type.
    public const string CreateIndex = $"""
        IF SERVERPROPERTY('IsFullTextInstalled') = 1
            AND NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('FailedMessages'))
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
