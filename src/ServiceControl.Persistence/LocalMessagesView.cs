namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Persistence.Infrastructure;

    /// <summary>
    /// Merges the failed and audited halves of one local result set under the three rules
    /// <see cref="IMessagesViewDataStore"/> states. A persister that holds both kinds of message
    /// returns them through this, so precedence, paging and counting are defined in one place rather
    /// than re-derived per provider and per query.
    /// </summary>
    public static class LocalMessagesView
    {
        public static QueryResult<IList<MessagesView>> Merge(
            IReadOnlyCollection<MessagesView> failedMessages,
            IReadOnlyCollection<MessagesView> auditedMessages,
            PagingInfo pagingInfo,
            IComparer<MessagesView>? order = null,
            DataVersion version = default)
        {
            ArgumentNullException.ThrowIfNull(failedMessages);
            ArgumentNullException.ThrowIfNull(auditedMessages);
            ArgumentNullException.ThrowIfNull(pagingInfo);

            var deduplicated = new Dictionary<string, MessagesView>(failedMessages.Count + auditedMessages.Count);

            // Failed first. A message that both failed and was audited must show as failed, and the
            // scatter gather deduplicates with TryAdd, so whichever row is seen first wins for good.
            foreach (var message in failedMessages.Concat(auditedMessages))
            {
                deduplicated.TryAdd(DeduplicationKey(message), message);
            }

            var merged = deduplicated.Values.ToList();

            if (order != null)
            {
                merged.Sort(order);
            }

            // The total is counted after deduplication, so a message that both failed and was audited
            // counts once rather than once per source.
            var totalCount = merged.Count;

            IList<MessagesView> page = merged.Take(pagingInfo.PageSize).ToList();

            return new QueryResult<IList<MessagesView>>(page, new QueryStatsInfo(version, totalCount));
        }

        /// <summary>
        /// The key <c>ScatterGatherApiMessageView</c> deduplicates on, so a local merge and a cross
        /// instance merge agree on what counts as the same message.
        /// </summary>
        public static string DeduplicationKey(MessagesView message) =>
            $"{message.ReceivingEndpoint?.Name}-{message.MessageId}";
    }
}
