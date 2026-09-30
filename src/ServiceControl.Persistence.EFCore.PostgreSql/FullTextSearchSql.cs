namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Full text search DDL for the failed messages table. EF Core cannot model a GIN index over an expression, so migrations apply it as SQL: AddFullTextSearch created the index, and WidenMessageIdAndCapFullTextBody replaced it with one that indexes only the start of the body. The statements live here, and not in the migrations themselves, so that regenerating the migrations with the dotnet-ef CLI only costs a one line migration body.
/// </summary>
static class FullTextSearchSql
{
    const string IndexName = "ix_failed_messages_full_text";
    const string TableName = "failed_messages";

    // 'simple' rather than 'english': message and header content is technical, stemming and
    // stopword removal do more harm than good.
    public const string Configuration = "simple";

    // to_tsvector fails once the vector it builds passes 1 MB, and the index builds one on every insert, so a body with enough distinct words would fail its whole ingestion batch. Only the start of the body is indexed, which keeps the vector well under that limit.
    public const int IndexedBodyLength = 262_144;

    // Written the way PostgreSqlFullTextSearchDialect makes EF Core render it, down to the casing
    // and the redundant looking parentheses: PostgreSQL only uses an expression index when the
    // query expression parses to the same tree, and a mismatch downgrades search to a sequential
    // scan silently. FullTextSearchIndexTests fails if the two drift apart.
    // The message type is indexed a second time with its separators replaced by spaces because the
    // default parser reads a dotted name as a single host token, so
    // "ServiceControl.MessageFailures.MyMessage" would not otherwise match a search for
    // "MyMessage". It mirrors the SearchableMessageType that MessageTypeEnricher produces for
    // RavenDB, and is not the duplicate of the headers it looks like.
    public static readonly string IndexedExpression =
        $"""to_tsvector('{Configuration}', headers_json || ' ' || substring(COALESCE(body_text, ''), 1, {IndexedBodyLength}) || ' ' || replace(replace(COALESCE(message_type, ''), '.', ' '), '+', ' '))""";

    // What AddFullTextSearch indexed. WidenMessageIdAndCapFullTextBody replaces it on the way up and restores it on the way down.
    const string UncappedIndexedExpression =
        $"""to_tsvector('{Configuration}', headers_json || ' ' || COALESCE(body_text, '') || ' ' || replace(replace(COALESCE(message_type, ''), '.', ' '), '+', ' '))""";

    public static readonly string CreateUncappedIndex = CreateIndexSql(null, UncappedIndexedExpression);

    public static readonly string CreateIndex = CreateIndexSql(null, IndexedExpression);

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
            var sql when sql == CreateUncappedIndex => WithSql(operation, CreateIndexSql(schema, UncappedIndexedExpression)),
            var sql when sql == CreateIndex => WithSql(operation, CreateIndexSql(schema, IndexedExpression)),
            var sql when sql == DropIndex => WithSql(operation, DropIndexSql(schema)),
            _ => operation
        };

    public static bool IsHandled(string sql) => sql == CreateUncappedIndex || sql == CreateIndex || sql == DropIndex;

    static string CreateIndexSql(string? schema, string indexedExpression) =>
        $"CREATE INDEX {IndexName} ON {Qualify(schema, TableName)} USING GIN ({indexedExpression})";

    // An index belongs to its table's schema, so it is the index that gets qualified here.
    static string DropIndexSql(string? schema) => $"DROP INDEX IF EXISTS {Qualify(schema, IndexName)}";

    static string Qualify(string? schema, string name) => schema is null ? name : $"\"{schema}\".{name}";

    static SqlOperation WithSql(SqlOperation operation, string sql) =>
        new() { Sql = sql, SuppressTransaction = operation.SuppressTransaction };
}
