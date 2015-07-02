namespace ServiceControl.Migrations
{
    using System;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Settings;

    [Migration(executionOrder: 201507011435)]
    public class ProcessedMessageMigration : Migration
    {
        readonly DateTime expiryThreshold;

        public ProcessedMessageMigration()
            : this(TimeSpan.FromHours(Settings.HoursToKeepMessagesBeforeExpiring))
        {
        }

        public ProcessedMessageMigration(TimeSpan timeToKeepMessagesBeforeExpiring)
        {
            expiryThreshold = SystemTime.UtcNow.Add(-timeToKeepMessagesBeforeExpiring);
        }

        protected override async Task UpAsyncInternal()
        {
            await DocumentStore.AsyncDatabaseCommands.UpdateByIndex(
                "Raven/DocumentsByEntityName",
                new IndexQuery
                {
                    Query = "Tag:ProcessedMessages"
                },
                new ScriptedPatchRequest()
                {
                    Script = @"
var processedMessageConverter = function($doc, $expiry)
{
    if(Date.parse($doc.ProcessedAt) > Date.parse($expiry))
    {
        PutDocument('AuditMessageSnapshots/' + $doc.UniqueMessageId,
                    { 
                        'AttemptedAt' : $doc.ProcessedAt,
                        'ProcessedAt' : $doc.ProcessedAt,
                        'ConversationId' : $doc.Headers['NServiceBus.CorrelationId'],
                        'IsSystemMessage' : $doc.MessageMetadata['IsSystemMessage'],
                        'MessageType' : $doc.MessageMetadata['MessageType'],
                        'Body' : {
                            '$type' : 'Particular.Backend.Debugging.BodyInformation, Particular.Backend.Debugging',
                            'BodyUrl' : $doc.MessageMetadata['BodyUrl'],
                            'ContentType' : $doc.MessageMetadata['ContentType'],
                            'ContentLength' : $doc.MessageMetadata['ContentLength'],
                            'Text' : $doc.MessageMetadata['Body']
                        },
                        'MessageIntent' : $doc.MessageMetadata['MessageIntent'],
                        'Processing' : {
                            '$type' : 'Particular.Backend.Debugging.ProcessingStatistics, Particular.Backend.Debugging',
                            'TimeSent' : $doc.MessageMetadata['TimeSent'],
                            'CriticalTime' : $doc.MessageMetadata['CriticalTime'],
                            'DeliveryTime' : $doc.MessageMetadata['DeliveryTime'],
                            'ProcessingTime' : $doc.MessageMetadata['ProcessingTime']
                        },
                        'ReceivingEndpoint' : $doc.MessageMetadata['ReceivingEndpoint'],
                        'SendingEndpoint' : $doc.MessageMetadata['SendingEndpoint'],
                        'HeadersForSearching' : $doc.MessageMetadata['HeadersForSearching'],
                        'MessageId' : $doc.MessageMetadata['MessageId'],
                        'UniqueMessageId' : $doc.UniqueMessageId,
                        'Status' : 3,
                        'Headers' : $doc.Headers
                    }, 
                    { 
                        'Raven-Entity-Name' : 'AuditMessageSnapshot',
                        'Raven-Clr-Type' : ' Particular.Backend.Debugging.RavenDB.Model.MessageSnapshotDocument,  Particular.Backend.Debugging.RavenDB'
                    }
        );
    }
};
processedMessageConverter(this, '" + expiryThreshold + "');"
                }
                , allowStale: true);
        }
    }
}