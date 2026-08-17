#nullable enable
namespace ServiceControl.Persistence
{
    using System.Threading;
    using System.Threading.Tasks;
    using ServiceControl.MessageFailures;

    public interface IEditFailedMessagesManager : IDataSessionManager
    {
        Task<FailedMessage?> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default);
        Task<string?> GetCurrentEditingRequestId(string failedMessageId, CancellationToken cancellationToken = default);
        Task SetCurrentEditingRequestId(string editingMessageId, CancellationToken cancellationToken = default);
        Task SetFailedMessageAsResolved(CancellationToken cancellationToken = default);
    }
}
