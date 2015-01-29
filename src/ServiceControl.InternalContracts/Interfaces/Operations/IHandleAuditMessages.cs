namespace ServiceControl.Operations
{
    using ServiceControl.Contracts.Operations;

    public interface IHandleAuditMessages
    {
        void Handle(ImportSuccessfullyProcessedMessage message);
    }
}