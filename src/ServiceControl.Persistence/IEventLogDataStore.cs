namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Infrastructure;

    public interface IEventLogDataStore
    {
        Task Add(EventLogItem logItem);
        Task<(IList<EventLogItem> items, long total, string version)> GetEventLogItems(PagingInfo pagingInfo);
    }
}