namespace ServiceControl.Migrations
{
    using System;
    using System.Threading.Tasks;
    using Raven.Abstractions;
    using Raven.Abstractions.Data;
    using ServiceBus.Management.Infrastructure.Settings;

    [Migration(executionOrder: 201507011435)]
    public class FailedMessageMigration : Migration
    {
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
}
historyConverter(this);

var snapshotConverter = function($doc, $expiry)
{
    var lastAttempt = $doc.ProcessingAttempts[$doc.ProcessingAttempts.length - 1];
    if(Date.parse(lastAttempt.AttemptedAt) > Date.parse($expiry))
    {
        PutDocument('AuditMessageSnapshots/' + $doc.UniqueMessageId,
                    { 
                        'AttemptedAt' : lastAttempt.AttemptedAt,
                        'ProcessedAt' : lastAttempt.AttemptedAt
                    }, 
                    { 
                        'Raven-Entity-Name' : 'AuditMessageSnapshot',
                        'Raven-Clr-Type' : ' Particular.Backend.Debugging.RavenDB.Model,  Particular.Backend.Debugging.RavenDB'
                    }
        );
    }
}
snapshotConverter(this, '" + SystemTime.UtcNow.Add(-TimeSpan.FromHours(Settings.HoursToKeepMessagesBeforeExpiring)) + "');"
                }
                , allowStale: true);
        }
    }
}