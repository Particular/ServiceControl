namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Transitions of <see cref="ServiceControl.MessageFailures.FailedMessage.Status" />.
    /// Every operation here has to update both StatusChangedAt and LastModified.
    /// </summary>
    public interface IFailedMessageLifecycleDataStore
    {
        Task MarkAsArchived(string failedMessageId, CancellationToken cancellationToken = default);
        Task<bool> MarkAsResolved(string failedMessageId, CancellationToken cancellationToken = default);
        Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds, CancellationToken cancellationToken = default);
        Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to, CancellationToken cancellationToken = default);
        Task RevertRetry(string messageUniqueId, CancellationToken cancellationToken = default);
    }
}
