namespace ServiceControl.Persistence.Sql;

using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceControl.EventLog;
using ServiceControl.Persistence;
using ServiceControl.Persistence.Infrastructure;

class NoOpEventLogDataStore : IEventLogDataStore
{
    public Task Add(EventLogItem logItem) => Task.CompletedTask;

    public Task<(IList<EventLogItem> items, long total, string version)> GetEventLogItems(PagingInfo pagingInfo) =>
        Task.FromResult<(IList<EventLogItem>, long, string)>(([], 0, string.Empty));
}
