namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.RavenDB.Indexes;

    public class IssueEndpointRetryAllHandler : IssueRetryAllHandlerBase, IHandleMessages<IssueEndpointRetryAll>
    {
        private string endpointName;

        public void Handle(IssueEndpointRetryAll message)
        {
            endpointName = message.EndpointName;
            ExecuteQuery();
        }

        protected override void AddWhere(IRavenQueryable<Messages_Ids.Result> query)
        {
            query.Where(r => r.ReceivingEndpointName == endpointName);
        }
    }
}