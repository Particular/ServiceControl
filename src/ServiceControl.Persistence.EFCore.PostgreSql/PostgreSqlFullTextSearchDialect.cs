namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class PostgreSqlFullTextSearchDialect : IFullTextSearchDialect
{
    // The tsvector expression has to be the one FullTextSearchSql indexes, character for character,
    // or the planner cannot use the GIN index and the search degrades to a sequential scan instead
    // of failing. FullTextSearchIndexTests pins the two together.
    // The document has to be built by concatenation: an interpolated string compiles to
    // string.Format, which EF Core cannot translate, and the query then throws.
    public IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms) =>
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

    // A quoted lexeme is parsed into a phrase of the words it contains, and :* makes each of them a
    // prefix. to_tsquery throws on an unescaped backslash or quote, and on an empty pair of quotes,
    // which is what a bare * would leave.
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
