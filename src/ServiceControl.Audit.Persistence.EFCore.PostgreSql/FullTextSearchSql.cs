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

    // Indexed expression using the search-document function. The dialect renders the same call
    // via HasDbFunction so the planner matches the query to the GIN index.
    const string FunctionName = "audit_message_search_document";

    public static readonly string FunctionIndexedExpression = $"{FunctionName}(headers_json, body_text, message_type)";

    public static readonly string CreateFunction = CreateFunctionSql(null);

    public static readonly string DropFunction = DropFunctionSql(null);

    public static readonly string CreateFunctionIndex = CreateFunctionIndexSql(null);

    public static MigrationOperation Rewrite(SqlOperation operation, string schema) =>
        operation.Sql switch
        {
            var sql when sql == Up => WithSql(operation, CreateIndexSql(schema)),
            var sql when sql == Down => WithSql(operation, DropIndexSql(schema)),
            var sql when sql == CreateFunction => WithSql(operation, CreateFunctionSql(schema)),
            var sql when sql == DropFunction => WithSql(operation, DropFunctionSql(schema)),
            var sql when sql == CreateFunctionIndex => WithSql(operation, CreateFunctionIndexSql(schema)),
            _ => operation
        };

    public static bool IsHandled(string sql) =>
        sql == Up || sql == Down
        || sql == CreateFunction || sql == DropFunction || sql == CreateFunctionIndex;

    static string CreateIndexSql(string? schema) =>
        $"CREATE INDEX {IndexName} ON {Qualify(schema, TableName)} USING GIN ({IndexedExpression})";

    static string DropIndexSql(string? schema) => $"DROP INDEX IF EXISTS {Qualify(schema, IndexName)}";

    // Builds the search document: filtered header JSON string values, capped body,
    // and normalized message type, as a tsvector using 'simple'.
    static string CreateFunctionSql(string? schema) =>
        $@"CREATE OR REPLACE FUNCTION {Qualify(schema, FunctionName)}(headers text, body text, message_type text)
RETURNS tsvector
LANGUAGE sql
IMMUTABLE
PARALLEL SAFE
AS $$
  SELECT jsonb_to_tsvector('{Configuration}'::regconfig,
         COALESCE(headers::jsonb, '{{}}'::jsonb)
           - ARRAY[
             'NServiceBus.MessageId','NServiceBus.ConversationId','NServiceBus.CorrelationId',
             'NServiceBus.RelatedTo','NServiceBus.TimeSent','NServiceBus.ProcessingStarted',
             'NServiceBus.ProcessingEnded','NServiceBus.DeliverAt','NServiceBus.Timeout.Expire',
             'NServiceBus.Retries.Timestamp','NServiceBus.ExceptionInfo.TimeOfFailure',
             'NServiceBus.TimeOfFailure','NServiceBus.NonDurableMessage','NServiceBus.TimeToBeReceived'
           ]::text[],
         '[""string""]'::jsonb)
       || to_tsvector('{Configuration}'::regconfig,
            substring(COALESCE(body, ''), 1, {IndexedBodyLength})
            || ' ' || replace(replace(COALESCE(message_type, ''), '.', ' '), '+', ' '))
$$;";

    static string DropFunctionSql(string? schema) =>
        $"DROP FUNCTION IF EXISTS {Qualify(schema, FunctionName)}(text, text, text)";

    static string FunctionIndexedExpressionForSchema(string? schema) =>
        $"{Qualify(schema, FunctionName)}(headers_json, body_text, message_type)";

    static string CreateFunctionIndexSql(string? schema) =>
        $"CREATE INDEX {IndexName} ON {Qualify(schema, TableName)} USING GIN ({FunctionIndexedExpressionForSchema(schema)})";

    static string Qualify(string? schema, string name) => schema is null ? name : $"\"{schema}\".{name}";

    static SqlOperation WithSql(SqlOperation operation, string sql) =>
        new() { Sql = sql, SuppressTransaction = operation.SuppressTransaction };
}
