namespace ServiceControl.Audit.Persistence.RavenDb.Indexes
{
    using System.Linq;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Audit.Auditing;
    using ServiceControl.Audit.Auditing.MessagesView;
    using ServiceControl.Audit.Monitoring;

    public class AuditCountIndex : AbstractIndexCreationTask<ProcessedMessage, DailyAuditCount>
    {
        public AuditCountIndex()
        {
            Map = messages => from message in messages
                              let count = (bool)message.MessageMetadata["IsSystemMessage"] ? 0 : 1
                              let endpointName = ((EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]).Name
                              let utcTime = message.ProcessedAt.ToUniversalTime().Date
                              select new DailyAuditCount
                              {
                                  UtcDate = utcTime,
                                  Data = new[]
                                  {
                                      new EndpointAuditCount { Name = endpointName, Count = count }
                                  }
                              };

            Reduce = results => from result in results
                                group result by result.UtcDate into timeGroup
                                select new DailyAuditCount
                                {
                                    UtcDate = timeGroup.Key,
                                    Data = timeGroup.SelectMany(res => res.Data).GroupBy(c => c.Name).Select(g => new EndpointAuditCount
                                    {
                                        Name = g.Key,
                                        Count = g.Sum(res => res.Count)
                                    })
                                    .ToArray()
                                };
        }
    }
}
