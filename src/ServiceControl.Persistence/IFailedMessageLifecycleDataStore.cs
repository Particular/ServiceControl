namespace ServiceControl.Persistence
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Transitions of <see cref="ServiceControl.MessageFailures.FailedMessage.Status" />.
    /// Every operation here has to update both StatusChangedAt and LastModified.
    /// </summary>
    public interface IFailedMessageLifecycleDataStore
    {
        Task MarkAsArchived(string failedMessageId);
        Task<bool> MarkAsResolved(string failedMessageId);
        Task<string[]> UnArchiveMessages(IEnumerable<string> failedMessageIds);
        Task<string[]> UnArchiveMessagesByRange(DateTime from, DateTime to);
        Task RevertRetry(string messageUniqueId);
    }
}
