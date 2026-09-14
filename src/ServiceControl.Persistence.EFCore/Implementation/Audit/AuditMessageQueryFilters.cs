namespace ServiceControl.Persistence.EFCore.Implementation.Audit;

using ServiceControl.Persistence.EFCore.Entities;
using ServiceControl.Persistence.Infrastructure;

static class AuditMessageQueryFilters
{
    public static IQueryable<AuditMessageEntity> IncludeSystemMessagesWhere(this IQueryable<AuditMessageEntity> source, bool includeSystemMessages) =>
        includeSystemMessages ? source : source.Where(message => !message.IsSystemMessage);

    public static IQueryable<AuditMessageEntity> FilterBySentTimeRange(this IQueryable<AuditMessageEntity> source, DateTimeRange? timeSentRange)
    {
        if (timeSentRange?.From is { } from)
        {
            source = source.Where(message => message.TimeSent >= from);
        }

        if (timeSentRange?.To is { } to)
        {
            source = source.Where(message => message.TimeSent <= to);
        }

        return source;
    }
}
