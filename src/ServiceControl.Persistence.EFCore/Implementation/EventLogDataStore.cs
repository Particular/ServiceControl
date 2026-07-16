namespace ServiceControl.Persistence.EFCore.Implementation;

using ServiceControl.EventLog;
using ServiceControl.Persistence.Infrastructure;

public class EventLogDataStore : IEventLogDataStore
{
    public Task Add(EventLogItem logItem) =>
        throw new NotImplementedException();

    public Task<(IList<EventLogItem> items, long total, string version)> GetEventLogItems(PagingInfo pagingInfo) =>
        throw new NotImplementedException();
}
