namespace ServiceControl.Persistence
{
    using System.Threading.Tasks;

    public interface IEventLogDataStore
    {
        Task Add(EventLog.EventLogItem logItem);
    }
}
