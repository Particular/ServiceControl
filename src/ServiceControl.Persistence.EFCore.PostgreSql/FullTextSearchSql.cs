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
    // The default parser reads a dotted name as a single host token, so
    // "ServiceControl.MessageFailures.MyMessage" would not match a search for "MyMessage". The
    // message type is therefore also indexed with its separators replaced by spaces, mirroring
    // the SearchableMessageType that MessageTypeEnricher already produces for RavenDB.
    public const string Up = $"""
        CREATE INDEX {IndexName}
            ON failed_messages
            USING GIN (to_tsvector('simple',
                coalesce(headers_json, '') || ' ' ||
                coalesce(body_text, '') || ' ' ||
                replace(replace(coalesce(message_type, ''), '.', ' '), '+', ' ')))
        """;

    public const string Down = $"DROP INDEX IF EXISTS {IndexName}";
}
