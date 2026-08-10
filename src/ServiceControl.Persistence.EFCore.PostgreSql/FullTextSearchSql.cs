namespace ServiceControl.Persistence.EFCore.PostgreSql;

/// <summary>
/// Full text search DDL for the failed messages table. EF Core cannot model a GIN index over an
/// expression, so it is applied by the AddFullTextSearch migration. The statements live here, and
/// not in the migration itself, so that regenerating the migrations with the dotnet-ef CLI only
/// costs a one line migration body.
/// </summary>
static class FullTextSearchSql
{
    const string IndexName = "ix_failed_messages_full_text";

    // 'simple' rather than 'english': message and header content is technical, stemming and
    // stopword removal do more harm than good.
    public const string Configuration = "simple";

    // Written the way PostgreSqlFullTextSearchDialect makes EF Core render it, down to the casing
    // and the redundant looking parentheses: PostgreSQL only uses an expression index when the
    // query expression parses to the same tree, and a mismatch downgrades search to a sequential
    // scan silently. FullTextSearchIndexTests fails if the two drift apart.
    // The message type is indexed a second time with its separators replaced by spaces because the
    // default parser reads a dotted name as a single host token, so
    // "ServiceControl.MessageFailures.MyMessage" would not otherwise match a search for
    // "MyMessage". It mirrors the SearchableMessageType that MessageTypeEnricher produces for
    // RavenDB, and is not the duplicate of the headers it looks like.
    public const string IndexedExpression =
        $"""to_tsvector('{Configuration}', headers_json || ' ' || COALESCE(body_text, '') || ' ' || replace(replace(COALESCE(message_type, ''), '.', ' '), '+', ' '))""";

    public const string Up = $"CREATE INDEX {IndexName} ON failed_messages USING GIN ({IndexedExpression})";

    public const string Down = $"DROP INDEX IF EXISTS {IndexName}";
}
