namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;

    public class IssueRetryAllHandler : IssueRetryAllHandlerBase, IHandleMessages<RequestRetryAll>
    {
        public void Handle(RequestRetryAll message)
        {
            ExecuteQuery();
        }
    }
}