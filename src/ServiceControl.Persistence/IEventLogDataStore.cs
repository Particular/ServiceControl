namespace ServiceControl.Persistence
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using EventLog;
    using Infrastructure;

    public interface IEventLogDataStore
    {
        /// <summary>
        /// Persists a single event log item. <paramref name="logItem"/> arrives fully formed from
        /// <c>EventLogMappingDefinition.Apply</c>, including its <c>Id</c>, which a persister must
        /// treat as opaque.
        /// </summary>
        Task Add(EventLogItem logItem);

        /// <summary>
        /// Returns one page of event log items, newest <c>RaisedAt</c> first.
        /// </summary>
        /// <param name="pagingInfo">Which page to return.</param>
        /// <param name="knownVersion">
        /// The version the caller already holds, or <c>null</c> if it holds none.
        /// </param>
        /// <returns>
        /// <c>items</c> — the requested page, which may be empty;
        /// <c>total</c> — the number of items in the store, independent of the page size;
        /// <c>version</c> — an opaque cache validator surfaced as the <c>ETag</c> response header by
        /// <c>EventLogApiController</c>.
        /// </returns>
        Task<(IList<EventLogItem> items, long total, string version)> GetEventLogItems(
            PagingInfo pagingInfo, string knownVersion = null);
    }
}