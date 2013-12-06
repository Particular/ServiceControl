namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;

    public class IssueRetryAllHandler : IssueRetryAllHandlerBase, IHandleMessages<IssueRetryAll>
    {
        public void Handle(IssueRetryAll message)
        {
            ExecuteQuery();
        }
    }
}