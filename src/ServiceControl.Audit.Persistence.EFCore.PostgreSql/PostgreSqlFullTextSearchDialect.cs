namespace ServiceControl.Audit.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class PostgreSqlFullTextSearchDialect : IFullTextSearchDialect
{
    public IQueryable<AuditMessageEntity> Search(IQueryable<AuditMessageEntity> source, string searchTerms) =>
        source.Where(message =>
            EF.Functions.ToTsVector(FullTextSearchSql.Configuration,
                    message.HeadersJson + " " +
                    (message.BodyText ?? "").Substring(0, FullTextSearchSql.IndexedBodyLength) + " " +
                    (message.MessageType ?? "").Replace(".", " ").Replace("+", " "))
                .Matches(EF.Functions.WebSearchToTsQuery(FullTextSearchSql.Configuration, ToOrQuery(searchTerms))));

    static string ToOrQuery(string searchTerms) =>
        string.Join(" OR ", searchTerms.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
