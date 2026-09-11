namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

/// <summary>
/// Full text search DDL for the failed messages table. EF Core cannot model a GIN index over an
/// expression, so it is applied by the AddFullTextSearch migration. The statements live here, and
/// not in the migration itself, so that regenerating the migrations with the dotnet-ef CLI only
/// costs a one line migration body.
/// </summary>
static class FullTextSearchSql
{
    const string IndexName = "ix_failed_messages_full_text";
    const string TableName = "failed_messages";

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

    public static readonly string Up = CreateIndexSql(null);

    public static readonly string Down = DropIndexSql(null);

    /// <summary>
    /// Re-renders the statement the migration carries, this time with the configured schema in it.
    /// Anything else is left alone: EF Core builds the migrations history table's own SQL through
    /// the same generator, already pointed at the right schema. MigrationSqlIsSchemaAwareTests is
    /// what catches a statement of ours that should have been listed here.
    /// </summary>
    public static MigrationOperation Rewrite(SqlOperation operation, string schema) =>
        operation.Sql switch
        {
            var sql when sql == Up => WithSql(operation, CreateIndexSql(schema)),
            var sql when sql == Down => WithSql(operation, DropIndexSql(schema)),
            _ => operation
        };

    public static bool IsHandled(string sql) => sql == Up || sql == Down;

    static string CreateIndexSql(string? schema) =>
        $"CREATE INDEX {IndexName} ON {Qualify(schema, TableName)} USING GIN ({IndexedExpression})";

    // An index belongs to its table's schema, so it is the index that gets qualified here.
    static string DropIndexSql(string? schema) => $"DROP INDEX IF EXISTS {Qualify(schema, IndexName)}";

    static string Qualify(string? schema, string name) => schema is null ? name : $"\"{schema}\".{name}";

    static SqlOperation WithSql(SqlOperation operation, string sql) =>
        new() { Sql = sql, SuppressTransaction = operation.SuppressTransaction };
}
