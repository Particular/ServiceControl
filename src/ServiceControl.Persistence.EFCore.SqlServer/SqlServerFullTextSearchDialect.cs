namespace ServiceControl.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.EFCore.Infrastructure;

class SqlServerFullTextSearchDialect : IFullTextSearchDialect
{
    // Both columns are covered by the index the AddFullTextSearch migration creates, and a NULL body
    // simply does not match.
    public IQueryable<FailedMessageEntity> Search(IQueryable<FailedMessageEntity> source, string searchTerms)
    {
        var searchCondition = ToPhrases(searchTerms);

        return source.Where(message =>
            EF.Functions.Contains(message.HeadersJson, searchCondition) ||
            EF.Functions.Contains(message.BodyText!, searchCondition));
    }

    // CONTAINS parses its argument as query syntax, where two bare words in a row are an error. A
    // quoted term is a phrase: the pieces the word breaker splits it into must appear together and
    // in order, and a trailing * makes it a prefix.
    static string ToPhrases(string searchTerms) =>
        string.Join(" OR ", searchTerms
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => $"\"{term.Replace("\"", "\"\"")}\""));
}
