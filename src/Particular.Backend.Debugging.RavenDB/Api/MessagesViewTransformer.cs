namespace Particular.Backend.Debugging.RavenDB.Api
{
    using System.Collections.Generic;
    using System.Linq;
    using Particular.Backend.Debugging.Api;
    using Raven.Client.Indexes;

    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessageSnapshot>
    {
        //public class Result
        //{
        //    public string Id { get; set; }
        //    public string UniqueMessageId { get; set; }
        //    public DateTime AttemptedAt { get; set; }
        //    public Dictionary<string, string> Headers { get; set; }
        //    public Dictionary<string, object> MessageMetadata { get; set; }
        //    public MessageStatus Status { get; set; }
        //}

        public MessagesViewTransformer()
        {
            TransformResults = messages => from message in messages
                let headers = message.Headers
                select new MessagesView
                {
                    Id = message.UniqueMessageId,
                    MessageId = message.MessageId,
                    MessageType = message.MessageType,
                    SendingEndpoint = message.SendingEndpoint,
                    ReceivingEndpoint = message.ReceivingEndpoint,
                    TimeSent = message.Processing.TimeSent,
                    ProcessedAt = message.AttemptedAt,
                    CriticalTime = message.Processing.CriticalTime,
                    ProcessingTime = message.Processing.ProcessingTime,
                    DeliveryTime = message.Processing.DeliveryTime,
                    IsSystemMessage = message.IsSystemMessage,
                    ConversationId = message.ConversationId,
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                    Status = message.Status,
                    MessageIntent =  message.MessageIntent,
                    BodyUrl = message.Body != null ? message.Body.BodyUrl : null,
                    BodySize = message.Body != null ? message.Body.ContentLenght : 0,
                    InvokedSagas = message.Sagas.InvokedSagas,
                    OriginatesFromSaga = message.Sagas.OriginatesFromSaga
                };
        }
    }
}