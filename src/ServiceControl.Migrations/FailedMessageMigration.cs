namespace ServiceControl.Migrations
{
    using System;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;

    [Migration(executionOrder: 201507011435)]
    public class FailedMessageMigration : Migration
    {
        readonly DateTime expiryThreshold;

        public FailedMessageMigration()
            : this(TimeSpan.FromHours(Settings.HoursToKeepMessagesBeforeExpiring))
        {
        }


        public FailedMessageMigration(TimeSpan timeToKeepMessagesBeforeExpiring)
        {
            expiryThreshold = SystemTime.UtcNow.Add(-timeToKeepMessagesBeforeExpiring);
        }

        public override async Task Up()
        {
            await DocumentStore.AsyncDatabaseCommands.UpdateByIndex(
                "Raven/DocumentsByEntityName",
                new IndexQuery
                {
                    Query = "Tag:FailedMessages"
                },
                new ScriptedPatchRequest()
                {
                    Script = @"
var historyConverter = function($doc) 
{
    var attempts = [];
    
    _($doc.ProcessingAttempts).forEach(function(attempt){
        attempts.push({
            'FailureDetails' : attempt.FailureDetails,
            'CorrelationId' : attempt.CorrelationId,
            'AttemptedAt' : attempt.AttemptedAt,
            'MessageId' : attempt.MessageId,
            'Headers' : attempt.Headers,
            'ReplyToAddress' : attempt.ReplyToAddress,
            'Recoverable' : attempt.Recoverable,
            'MessageIntent' : attempt.MessageIntent,
            'SendingEndpoint' : attempt.MessageMetadata['SendingEndpoint'],
            'ProcessingEndpoint' : attempt.MessageMetadata['ReceivingEndpoint'],
            'ContentType' : attempt.MessageMetadata['ContentType'],
            'IsSystemMessage' : attempt.MessageMetadata['IsSystemMessage'],
            'MessageType' : attempt.MessageMetadata['MessageType'],
            'TimeSent' : attempt.MessageMetadata['TimeSent']   
        });
    });
    
    PutDocument('MessageFailureHistories/' + $doc.UniqueMessageId,
                { 
                    'Status' : $doc.Status,
                    'UniqueMessageId' : $doc.UniqueMessageId,
                    'ProcessingAttempts' : attempts 
                }, 
                { 
                    'Raven-Entity-Name' : 'MessageFailureHistories',
                    'Raven-Clr-Type' : 'ServiceControl.MessageFailures.MessageFailureHistory, ServiceControl'
                }
    );
};
historyConverter(this);

var snapshotConverter = function($doc, $expiry)
{
    var lastAttempt = $doc.ProcessingAttempts[$doc.ProcessingAttempts.length - 1];
    if(Date.parse(lastAttempt.AttemptedAt) > Date.parse($expiry))
    {
        PutDocument('AuditMessageSnapshots/' + $doc.UniqueMessageId,
                    { 
                        'AttemptedAt' : lastAttempt.AttemptedAt,
                        'ProcessedAt' : lastAttempt.AttemptedAt,
                        'ConversationId' : lastAttempt.CorrelationId,
                        'IsSystemMessage' : lastAttempt.MessageMetadata['IsSystemMessage'],
                        'MessageType' : lastAttempt.MessageMetadata['MessageType'],
                        'Body' : {
                            '$type' : 'Particular.Backend.Debugging.BodyInformation, Particular.Backend.Debugging',
                            'BodyUrl' : lastAttempt.MessageMetadata['BodyUrl'],
                            'ContentType' : lastAttempt.MessageMetadata['ContentType'],
                            'ContentLength' : lastAttempt.MessageMetadata['ContentLength'],
                            'Text' : lastAttempt.MessageMetadata['Body']
                        },
                        'MessageIntent' : lastAttempt.MessageIntent,
                        'Processing' : {
                            '$type' : 'Particular.Backend.Debugging.ProcessingStatistics, Particular.Backend.Debugging',
                            'TimeSent' : lastAttempt.MessageMetadata['TimeSent'],
                            'CriticalTime' : lastAttempt.MessageMetadata['CriticalTime'],
                            'DeliveryTime' : lastAttempt.MessageMetadata['DeliveryTime'],
                            'ProcessingTime' : lastAttempt.MessageMetadata['ProcessingTime']
                        },
                        'ReceivingEndpoint' : lastAttempt.MessageMetadata['ReceivingEndpoint'],
                        'SendingEndpoint' : lastAttempt.MessageMetadata['SendingEndpoint'],
                        'HeadersForSearching' : lastAttempt.MessageMetadata['HeadersForSearching'],
                        'MessageId' : lastAttempt.MessageId,
                        'UniqueMessageId' : $doc.UniqueMessageId,
                        'Status' : 1,
                        'Headers' : lastAttempt.Headers
                    }, 
                    { 
                        'Raven-Entity-Name' : 'AuditMessageSnapshot',
                        'Raven-Clr-Type' : ' Particular.Backend.Debugging.RavenDB.Model.MessageSnapshotDocument,  Particular.Backend.Debugging.RavenDB'
                    }
        );
    }
};
snapshotConverter(this, '" + expiryThreshold + "');"
                }
                , allowStale: true);
        }

        MessageStatus ConvertStatus(FailedMessageStatus status)
        {
            switch (status)
            {
                case FailedMessageStatus.Archived:
                    return MessageStatus.ArchivedFailure;
                case FailedMessageStatus.Resolved:
                    return MessageStatus.ResolvedSuccessfully;
                default:
                    return MessageStatus.Failed;
            }
        }
    }
}