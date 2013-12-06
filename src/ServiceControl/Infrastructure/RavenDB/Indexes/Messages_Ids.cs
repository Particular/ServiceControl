namespace ServiceBus.Management.Infrastructure.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Client.Indexes;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageAuditing;

    public class Messages_Ids : AbstractIndexCreationTask<AuditMessage, Messages_Ids.Result>
    {
        public class Result
        {
            public string Id { get; set; }
            public string ReceivingEndpointName { get; set; }
        }

        public Messages_Ids()
        {
            Map = messages => from message in messages
                where message.Status != MessageStatus.Successful &&
                      message.Status != MessageStatus.RetryIssued
                select new
                {
                    message.Id,
                    ReceivingEndpointName = message.ReceivingEndpoint.Name
                };
        }
    }
}