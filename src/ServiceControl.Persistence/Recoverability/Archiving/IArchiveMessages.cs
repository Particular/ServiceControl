namespace ServiceControl.Persistence.Recoverability
{
    using System.Threading.Tasks;
    using ServiceControl.Recoverability;

    /// <summary>
    /// Implementers of this interface are expected to emit domain events as well
    /// </summary>
    public interface IArchiveMessages
    {
        Task ArchiveAllInGroup(string groupId);
        Task UnarchiveAllInGroup(string groupId);
        bool IsOperationInProgressFor(string groupId, ArchiveType archiveType);
        Task StartArchiving(string groupId, ArchiveType archiveType);
    }
}
