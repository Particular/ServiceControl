namespace ServiceBus.Management.Handlers
{
    using Commands;
    using NServiceBus;

    public class IssueRetryAllHandler : IssueRetryAllHandlerBase, IHandleMessages<IssueRetryAll>
    {
        public void Handle(IssueRetryAll message)
        {
            ExecuteQuery();
        }
    }
}