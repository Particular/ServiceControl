namespace ServiceControl.Persistence
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Infrastructure;

    public interface IEventLogDataStore
    {
        Task Add(EventLogItem logItem);
        Task<(IList<EventLogItem> items, int total, string version)> GetEventLogItems(PagingInfo pagingInfo);
    }
}
