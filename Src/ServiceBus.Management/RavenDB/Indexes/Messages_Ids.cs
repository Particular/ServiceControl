namespace ServiceBus.Management.RavenDB.Indexes
{
    using System.Linq;
    using Raven.Client.Indexes;

    public class Messages_Ids : AbstractIndexCreationTask<Message, Messages_Ids.Result>
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