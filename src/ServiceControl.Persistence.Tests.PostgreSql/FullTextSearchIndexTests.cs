namespace ServiceControl.Persistence.Tests;

using System.Text.RegularExpressions;
using EFCore.PostgreSql;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

/// <summary>
/// PostgreSQL only uses the GIN index of the AddFullTextSearch migration when the query expression
/// parses to the same tree as the indexed one. A mismatch is silent: search keeps working, on a
/// sequential scan of every failed message. These tests need no database.
/// </summary>
class FullTextSearchIndexTests
{
    [Test]
    public void Search_uses_the_indexed_expression()
    {
        var sql = WithoutTableAlias(SearchQuery("forty-two"));

        Assert.That(sql, Does.Contain(FullTextSearchSql.IndexedExpression));
    }

    [Test]
    public void Audit_search_uses_the_indexed_expression()
    {
        var sql = WithoutTableAlias(AuditSearchQuery("forty-two"), "audit_messages");

        Assert.That(sql, Does.Contain(FullTextSearchSql.IndexedExpression));
    }

    [Test]
    public void Terms_are_ored()
    {
        var sql = SearchQuery("forty two");

        Assert.That(sql, Does.Contain("='forty OR two'"));
    }

    static string SearchQuery(string searchTerms)
    {
        using var dbContext = CreateDbContext();

        return new PostgreSqlFullTextSearchDialect()
            .Search(dbContext.FailedMessages, searchTerms)
            .ToQueryString();
    }

    static string AuditSearchQuery(string searchTerms)
    {
        using var dbContext = CreateDbContext();

        return new PostgreSqlFullTextSearchDialect()
            .Search(dbContext.AuditMessages, searchTerms)
            .ToQueryString();
    }

    static PostgreSqlServiceControlDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PostgreSqlServiceControlDbContext>()
            .UseNpgsql("Host=localhost;Database=servicecontrol")
            .Options;

        return new PostgreSqlServiceControlDbContext(options);
    }

    // The DDL names the columns bare, the query qualifies them with whatever alias EF picked.
    static string WithoutTableAlias(string sql, string table = "failed_messages")
    {
        var alias = Regex.Match(sql, $@"FROM {table} AS (\w+)").Groups[1].Value;

        return sql.Replace($"{alias}.", string.Empty);
    }
}
