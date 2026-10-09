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
                .Matches(EF.Functions.ToTsQuery(FullTextSearchSql.Configuration, ToOrQuery(searchTerms))));

    static string ToOrQuery(string searchTerms) =>
        string.Join(" | ", searchTerms
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ToPhrase)
            .OfType<string>());

    static string? ToPhrase(string term)
    {
        var word = term.TrimEnd('*');
        if (word.Length == 0)
        {
            return null;
        }

        var phrase = $"'{word.Replace(@"\", @"\\").Replace("'", "''")}'";

        return word.Length < term.Length ? phrase + ":*" : phrase;
    }
}
