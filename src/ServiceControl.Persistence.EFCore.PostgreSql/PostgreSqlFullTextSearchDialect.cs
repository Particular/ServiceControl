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
                    (message.BodyText ?? "") + " " +
                    (message.MessageType ?? "").Replace(".", " ").Replace("+", " "))
                .Matches(EF.Functions.WebSearchToTsQuery(FullTextSearchSql.Configuration, ToOrQuery(searchTerms))));

    // websearch_to_tsquery ANDs bare terms; the RavenDB persister ORs them, so the terms are
    // rejoined with the operator that syntax understands. It also never throws on odd input, which
    // a hand built tsquery would.
    static string ToOrQuery(string searchTerms) =>
        string.Join(" OR ", searchTerms.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
