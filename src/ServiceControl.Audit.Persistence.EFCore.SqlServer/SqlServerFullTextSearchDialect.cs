namespace ServiceControl.Audit.Persistence.EFCore.SqlServer;

using Microsoft.EntityFrameworkCore;
using ServiceControl.Audit.Persistence.EFCore.Entities;
using ServiceControl.Audit.Persistence.EFCore.Infrastructure;

class SqlServerFullTextSearchDialect : IFullTextSearchDialect
{
    public IQueryable<AuditMessageEntity> Search(IQueryable<AuditMessageEntity> source, string searchTerms)
    {
        var searchCondition = ToPhrases(searchTerms);

        return source.Where(message =>
            EF.Functions.Contains(message.HeadersJson, searchCondition) ||
            EF.Functions.Contains(message.BodyText!, searchCondition));
    }

    static string ToPhrases(string searchTerms) =>
        string.Join(" OR ", searchTerms
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => $"\"{term.Replace("\"", "\"\"")}\""));
}
