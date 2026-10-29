namespace ServiceControl.Persistence.EFCore.PostgreSql;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class PostgreSqlFullTextSearchDialect : IFullTextSearchDialect
{
    // The function call must produce the same expression the GIN index uses, or the planner cannot
    // match the query to the index and search degrades to a sequential scan without failing.
    // FullTextSearchIndexTests pins the two together. Body cap and message-type normalization are
    // in the SQL function (single source of truth), so the dialect no longer applies them.
    public IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms) =>
        source.Where(message =>
            SearchDocumentFunction.SearchDocument(message.HeadersJson, message.BodyText, message.MessageType)
                .Matches(EF.Functions.ToTsQuery(FullTextSearchSql.Configuration, ToOrQuery(searchTerms)))
            || message.MessageId == searchTerms
            || message.ConversationId == searchTerms);

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
