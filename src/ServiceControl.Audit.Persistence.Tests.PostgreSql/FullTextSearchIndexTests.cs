namespace ServiceControl.Audit.Persistence.Tests
{
    using System.Text.RegularExpressions;
    using Microsoft.EntityFrameworkCore;
    using NUnit.Framework;
    using ServiceControl.Audit.Persistence.EFCore.PostgreSql;

    class FullTextSearchIndexTests
    {
        [Test]
        public void Search_uses_the_indexed_expression()
        {
            var sql = SearchQuery("forty-two");
            var alias = Regex.Match(sql, @"FROM audit_messages AS (\w+)").Groups[1].Value;

            Assert.That(sql.Replace($"{alias}.", string.Empty), Does.Contain(FullTextSearchSql.IndexedExpression));
        }

        [Test]
        public void Terms_are_ored()
        {
            Assert.That(SearchQuery("forty two"), Does.Contain("='forty OR two'"));
        }

        static string SearchQuery(string searchTerms)
        {
            using var dbContext = new PostgreSqlAuditDbContextFactory().CreateDbContext([]);

            return new PostgreSqlFullTextSearchDialect()
                .Search(dbContext.AuditMessages, searchTerms)
                .ToQueryString();
        }
    }
}
