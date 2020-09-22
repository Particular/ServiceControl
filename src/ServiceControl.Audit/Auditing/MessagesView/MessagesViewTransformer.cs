// TODO: RAVEN5 - No Transformers
//namespace ServiceControl.Audit.Auditing.MessagesView
//{
//    using System;
//    using System.Collections.Generic;
//    using System.Linq;
//    using Monitoring;
//    using Raven.Client.Indexes;

//    public class MessagesViewTransformer : AbstractTransformerCreationTask<MessagesViewTransformer.Result>
//    {
//        public MessagesViewTransformer()
//        {
//            TransformResults = messages => from message in messages
//                where message.ProcessedAt != null // necessary to avoid NullReferenceException deep in raven black magic
//                let metadata = message.MessageMetadata
//                let headers = message.Headers
//                let processedAt = message.ProcessedAt
//                let status = !(bool)message.MessageMetadata["IsRetried"] ? MessageStatus.Successful : MessageStatus.ResolvedSuccessfully
//                select new
//                {
//                    Id = message.UniqueMessageId,
//                    MessageId = metadata["MessageId"],
//                    MessageType = metadata["MessageType"],
//                    SendingEndpoint = metadata["SendingEndpoint"],
//                    ReceivingEndpoint = metadata["ReceivingEndpoint"],
//                    TimeSent = (DateTime?)metadata["TimeSent"],
//                    ProcessedAt = processedAt,
//                    CriticalTime = (TimeSpan)metadata["CriticalTime"],
//                    ProcessingTime = (TimeSpan)metadata["ProcessingTime"],
//                    DeliveryTime = (TimeSpan)metadata["DeliveryTime"],
//                    IsSystemMessage = (bool)metadata["IsSystemMessage"],
//                    ConversationId = metadata["ConversationId"],
//                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
//                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
//                    Headers = headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
//                    Status = status,
//                    MessageIntent = metadata["MessageIntent"],
//                    BodyUrl = metadata["BodyUrl"],
//                    BodySize = (int)metadata["ContentLength"],
//                    InvokedSagas = metadata["InvokedSagas"],
//                    OriginatesFromSaga = metadata["OriginatesFromSaga"]
//                };
//        }

//        public class Result
//        {
//            public string Id { get; set; }
//            public string UniqueMessageId { get; set; }
//            public DateTime ProcessedAt { get; set; }
//            public Dictionary<string, string> Headers { get; set; }
//            public Dictionary<string, object> MessageMetadata { get; set; }
//        }
//    }
//}