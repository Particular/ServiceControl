namespace ServiceControl.Audit.Persistence.RavenDb.Indexes
{
    using System;
    using System.Linq;
    using Lucene.Net.Analysis.Standard;
    using Monitoring;
    using Raven.Client.Documents.Indexes;
    using ServiceControl.Audit.Auditing;

    public class MessagesViewIndexWithFullTextSearch : AbstractIndexCreationTask<ProcessedMessage, MessagesViewIndex.SortAndFilterOptions>
    {
        public MessagesViewIndexWithFullTextSearch()
        {
            Map = messages => from message in messages
                              select new MessagesViewIndex.SortAndFilterOptions
                              {
                                  MessageId = (string)message.MessageMetadata["MessageId"],
                                  MessageType = (string)message.MessageMetadata["MessageType"],
                                  IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                                  Status = (bool)message.MessageMetadata["IsRetried"] ? MessageStatus.ResolvedSuccessfully : MessageStatus.Successful,
                                  TimeSent = (DateTime)message.MessageMetadata["TimeSent"],
                                  ProcessedAt = message.ProcessedAt,
                                  ReceivingEndpointName = ((EndpointDetails)message.MessageMetadata["ReceivingEndpoint"]).Name,
                                  CriticalTime = (TimeSpan?)message.MessageMetadata["CriticalTime"],
                                  ProcessingTime = (TimeSpan?)message.MessageMetadata["ProcessingTime"],
                                  DeliveryTime = (TimeSpan?)message.MessageMetadata["DeliveryTime"],
                                  Query = message.MessageMetadata.Select(_ => _.Value.ToString()).Union(new[]
                                  {
                                    string.Join(" ", message.Headers.Select(x => x.Value)),
                                    LoadAttachment(message, "body").GetContentAsString()
                                }).ToArray(),
                                  ConversationId = (string)message.MessageMetadata["ConversationId"]
                              };

            Index(x => x.Query, FieldIndexing.Search);

            Analyze(x => x.Query, typeof(StandardAnalyzer).AssemblyQualifiedName);
        }
    }
}