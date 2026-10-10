namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore.Migrations.Operations;

static class FullTextSearchSql
{
    const string IndexName = "ix_audit_messages_full_text";
    const string TableName = "audit_messages";

    public const string Configuration = "simple";

    // to_tsvector fails once a document's lexemes pass 1 MB.
    public const int IndexedBodyLength = 262144;

    // Must match what EF renders for PostgreSqlFullTextSearchDialect exactly, or PostgreSQL won't use the index.
    public static readonly string IndexedExpression =
        $"""to_tsvector('{Configuration}', headers_json || ' ' || substring(COALESCE(body_text, ''), 1, {IndexedBodyLength}) || ' ' || replace(replace(COALESCE(message_type, ''), '.', ' '), '+', ' '))""";

    public static readonly string Up = CreateIndexSql(null);

    public static readonly string Down = DropIndexSql(null);

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

    static string DropIndexSql(string? schema) => $"DROP INDEX IF EXISTS {Qualify(schema, IndexName)}";

    static string Qualify(string? schema, string name) => schema is null ? name : $"\"{schema}\".{name}";

    static SqlOperation WithSql(SqlOperation operation, string sql) =>
        new() { Sql = sql, SuppressTransaction = operation.SuppressTransaction };
}
