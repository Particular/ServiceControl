namespace ServiceControl.Persistence.Recoverability
{
    using System.Threading.Tasks;
    using ServiceControl.Infrastructure.DomainEvents;
    using ServiceControl.Recoverability;

    public interface IArchiveMessages
    {
        Task ArchiveAllInGroup(string groupId, IDomainEvents domainEvents);
        Task UnarchiveAllInGroup(string groupId, IDomainEvents domainEvents);
        bool IsOperationInProgressFor(string groupId, ArchiveType archiveType);
        Task StartArchiving(string groupId, ArchiveType archiveType);
    }
}
