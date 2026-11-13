namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

// Maps the PostgreSQL search-document function so EF Core renders a schema-qualified function call
// in the query instead of an inline to_tsvector expression. The function builds the same document
// (filtered headers, capped body, normalized message type) so the query expression matches the
// GIN expression index created from FullTextSearchSql.FunctionIndexedExpression.
static class SearchDocumentFunction
{
    public const string Name = "audit_message_search_document";

    // Never executed; EF Core translates the call to SQL. Throwing keeps a stray direct call loud.
    public static NpgsqlTsVector SearchDocument(string headersJson, string? bodyText, string? messageType) =>
        throw new NotSupportedException();

    public static void Apply(ModelBuilder modelBuilder, string? schema)
    {
        var function = modelBuilder
            .HasDbFunction(typeof(SearchDocumentFunction).GetMethod(nameof(SearchDocument))!)
            .HasName(Name);
        if (schema is not null)
        {
            function.HasSchema(schema);
        }
    }
}