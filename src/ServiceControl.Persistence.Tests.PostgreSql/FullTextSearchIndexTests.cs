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
    public void Terms_are_ored()
    {
        var sql = SearchQuery("forty two");

        Assert.That(sql, Does.Contain("='forty OR two'"));
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

    // The DDL names the columns bare, the query qualifies them with whatever alias EF picked.
    static string WithoutTableAlias(string sql)
    {
        var alias = Regex.Match(sql, @"FROM failed_messages AS (\w+)").Groups[1].Value;

        return sql.Replace($"{alias}.", string.Empty);
    }
}
