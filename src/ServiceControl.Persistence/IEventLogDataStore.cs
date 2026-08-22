namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using EventLog;
    using Infrastructure;

    /// <summary>
    /// Stores and reads the event log.
    /// </summary>
    public interface IEventLogDataStore
    {
        /// <summary>
        /// Persists a single event log item. Identity is the persister's to assign, and is surfaced
        /// on <see cref="EventLogItemView.Id"/> when the item is read back.
        /// </summary>
        /// <param name="logItem">The item to store.</param>
        Task Add(EventLogItem logItem, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns one page of event log items, newest <c>RaisedAt</c> first.
        /// </summary>
        /// <param name="pagingInfo">Which page to return.</param>
        Task<QueryResult<IList<EventLogItemView>>> GetEventLogItems(
            PagingInfo pagingInfo, CancellationToken cancellationToken = default);
    }
}