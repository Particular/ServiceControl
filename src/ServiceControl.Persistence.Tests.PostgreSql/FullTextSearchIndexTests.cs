namespace ServiceControl.Persistence.Tests;

using System.Linq;
using System.Text.RegularExpressions;
using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NUnit.Framework;
using ServiceControl.Persistence.EFCore.Infrastructure;

/// <summary>
/// PostgreSQL only uses the full text GIN index when the query expression parses to the same tree as the indexed one. A mismatch is silent: search keeps working, on a sequential scan of every failed message. These tests need no database.
/// </summary>
class FullTextSearchIndexTests
{
    [Test]
    public void Search_uses_the_indexed_expression()
    {
        var sql = WithoutTableAlias(SearchQuery("forty-two"));

        Assert.That(sql, Does.Contain(FullTextSearchSql.FunctionIndexedExpression));
    }

    [Test]
    public void Terms_are_ored()
    {
        var sql = SearchQuery("forty two");

        Assert.That(sql, Does.Contain("'forty' | 'two'"));
    }

    [Test]
    public void A_trailing_star_makes_a_term_a_prefix()
    {
        var sql = SearchQuery("forty*");

        Assert.That(sql, Does.Contain("'forty':*"));
    }

    [Test]
    public void Search_with_schema_uses_schema_qualified_function()
    {
        var sql = WithoutTableAlias(SearchQueryWithSchema("forty-two", "svc"));

        Assert.That(sql, Does.Contain("svc.failed_message_search_document("));
    }

    [Test]
    public void Exact_id_arms_are_present()
    {
        // Narrow projection excludes message_id and conversation_id from SELECT,
        // so they only appear when the exact-ID OR arms are in the WHERE clause.
        var options = new DbContextOptionsBuilder<PostgreSqlServiceControlDbContext>()
            .UseNpgsql("Host=localhost;Database=servicecontrol")
            .Options;
        using var dbContext = new PostgreSqlServiceControlDbContext(options);

        var sql = WithoutTableAlias(
            new PostgreSqlFullTextSearchDialect()
                .Search(dbContext.FailedMessages, "a b")
                .Select(m => new { m.Status })
                .ToQueryString());

        Assert.That(sql, Does.Contain("message_id"), "MessageId exact-match arm is missing");
        Assert.That(sql, Does.Contain("conversation_id"), "ConversationId exact-match arm is missing");
    }

    [Test]
    public void Function_definition_uses_native_jsonb_to_tsvector_with_string_filter()
    {
        Assert.That(FullTextSearchSql.CreateFunction, Does.Contain("jsonb_to_tsvector"));
        Assert.That(FullTextSearchSql.CreateFunction, Does.Contain("""["string"]"""));
    }

    [Test]
    public void Function_definition_excludes_approved_header_keys()
    {
        string[] approvedKeys =
        [
            "NServiceBus.MessageId", "NServiceBus.ConversationId", "NServiceBus.CorrelationId",
            "NServiceBus.RelatedTo", "NServiceBus.TimeSent", "NServiceBus.ProcessingStarted",
            "NServiceBus.ProcessingEnded", "NServiceBus.DeliverAt", "NServiceBus.Timeout.Expire",
            "NServiceBus.Retries.Timestamp", "NServiceBus.ExceptionInfo.TimeOfFailure",
            "NServiceBus.TimeOfFailure", "NServiceBus.NonDurableMessage", "NServiceBus.TimeToBeReceived"
        ];

        foreach (var key in approvedKeys)
        {
            Assert.That(FullTextSearchSql.CreateFunction, Does.Contain($"'{key}'"),
                $"Approved exclusion key '{key}' is missing from the function definition");
        }
    }

    [Test]
    public void Function_index_ddl_contains_indexed_expression()
    {
        Assert.That(FullTextSearchSql.CreateFunctionIndex,
            Does.Contain(FullTextSearchSql.FunctionIndexedExpression),
            "The function-call GIN index DDL must contain the same expression the query renders");
    }

    [Test]
    public void Function_index_and_drop_rewrite_with_schema()
    {
        var rewrittenIndex = (SqlOperation)FullTextSearchSql.Rewrite(
            new SqlOperation { Sql = FullTextSearchSql.CreateFunctionIndex }, "svc");
        var rewrittenDrop = (SqlOperation)FullTextSearchSql.Rewrite(
            new SqlOperation { Sql = FullTextSearchSql.DropFunction }, "svc");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(rewrittenIndex.Sql, Does.Contain("\"svc\".failed_messages"),
                "CreateFunctionIndex must qualify the table with the schema");
            Assert.That(rewrittenIndex.Sql, Does.Contain("\"svc\".failed_message_search_document"),
                "CreateFunctionIndex must qualify the function with the schema");
            Assert.That(rewrittenDrop.Sql, Does.Contain("\"svc\".failed_message_search_document"),
                "DropFunction must qualify the function with the schema");
        }
    }

    [Test]
    public void Function_definition_rewrites_with_schema()
    {
        var rewritten = (SqlOperation)FullTextSearchSql.Rewrite(
            new SqlOperation { Sql = FullTextSearchSql.CreateFunction }, "svc");

        Assert.That(rewritten.Sql, Does.Contain("\"svc\".failed_message_search_document"));
    }

    static string SearchQuery(string searchTerms)
    {
        var options = new DbContextOptionsBuilder<PostgreSqlServiceControlDbContext>()
            .UseNpgsql("Host=localhost;Database=servicecontrol")
            .Options;

        using var dbContext = new PostgreSqlServiceControlDbContext(options);

        return new PostgreSqlFullTextSearchDialect()
            .Search(dbContext.FailedMessages, searchTerms)
            .ToQueryString();
    }

    static string SearchQueryWithSchema(string searchTerms, string schema)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlServiceControlDbContext>()
            .UseNpgsql("Host=localhost;Database=servicecontrol");
        ((IDbContextOptionsBuilderInfrastructure)optionsBuilder).AddOrUpdateExtension(new SchemaOptionsExtension(schema));

        using var dbContext = new PostgreSqlServiceControlDbContext(optionsBuilder.Options);

        return new PostgreSqlFullTextSearchDialect()
            .Search(dbContext.FailedMessages, searchTerms)
            .ToQueryString();
    }

    // The DDL names the columns bare, the query qualifies them with whatever alias EF picked.
    static string WithoutTableAlias(string sql)
    {
        var alias = Regex.Match(sql, @"FROM (?:\w+\.)?failed_messages AS (\w+)").Groups[1].Value;

        return sql.Replace($"{alias}.", string.Empty);
    }
}
