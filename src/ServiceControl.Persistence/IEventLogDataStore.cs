namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Infrastructure;

    /// <summary>
    /// Stores and reads the event log.
    /// <para>
    /// Items are <b>subject to provider retention and disappear on their own</b>. Each persister
    /// enforces this its own way and neither is visible here. RavenDB stamps per-document expiry
    /// metadata when the item is written, EF Core deletes aged rows on a timer.
    /// </para>
    /// </summary>
    public interface IEventLogDataStore
    {
        /// <summary>
        /// Persists a single event log item. Identity is the persister's to assign, and is surfaced
        /// on <see cref="EventLogItemView.Id"/> when the item is read back.
        /// </summary>
        /// <param name="logItem">The item to store.</param>
        Task Add(EventLogItem logItem);

        /// <summary>
        /// Returns one page of event log items, newest <c>RaisedAt</c> first.
        /// </summary>
        /// <param name="pagingInfo">Which page to return.</param>
        /// <param name="knownVersion">
        /// What the caller already holds, or <see cref="DataVersion.None"/>. On a match the result is
        /// <see cref="QueryResult{TOut}.NotModified"/> and carries no page.
        /// </param>
        Task<QueryResult<IList<EventLogItemView>>> GetEventLogItems(
            PagingInfo pagingInfo, DataVersion knownVersion = default);
    }
}