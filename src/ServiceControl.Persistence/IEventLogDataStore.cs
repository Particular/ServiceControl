namespace ServiceControl.Persistence
{
    using System;
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
        /// Persists a single event log item.
        /// </summary>
        /// <param name="logItem">The item to store.</param>
        /// <param name="eventId">
        /// The item's portable/global identity, minted by the caller, and survives a
        /// move between persisters.
        /// </param>
        Task Add(EventLogItem logItem, Guid eventId);

        /// <summary>
        /// Returns one page of event log items, newest <c>RaisedAt</c> first.
        /// </summary>
        /// <param name="pagingInfo">Which page to return.</param>
        /// <param name="knownVersion">
        /// The version the caller already holds, or <c>null</c> if it holds none.
        /// </param>
        /// <returns>
        /// <c>items</c>: the requested page, which may be empty;
        /// <c>total</c>: the number of items in the store, independent of the page size;
        /// <c>version</c>: an opaque cache validator surfaced as the <c>ETag</c> response header by
        /// <c>EventLogApiController</c>. It must change when retention removes items, not only when
        /// one is added, since nothing else tells a client its cached page is now wrong.
        /// </returns>
        Task<(IList<EventLogItemView> items, long total, string version)> GetEventLogItems(
            PagingInfo pagingInfo, string knownVersion = null);
    }
}