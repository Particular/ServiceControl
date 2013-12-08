namespace ServiceControl.MessageFailures.Handlers
{
    using InternalMessages;
    using NServiceBus;
    using Raven.Client.Linq;
    using ServiceBus.Management.Infrastructure.RavenDB.Indexes;

    public class IssueEndpointRetryAllHandler : IssueRetryAllHandlerBase, IHandleMessages<RequestEndpointRetryAll>
    {
        private string endpointName;

        public void Handle(RequestEndpointRetryAll message)
        {
            endpointName = message.EndpointName;
            ExecuteQuery();
        }

        //protected override void AddWhere(IRavenQueryable<Messages_Ids.Result> query)
        //{
        //    query.Where(r => r.ReceivingEndpointName == endpointName);
        //}
    }
}