namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.Http;
    using Audit.Auditing;
    using Audit.Auditing.MessagesView;
    using Audit.Monitoring;
    using NServiceBus;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;
    using SagaAudit;

    static class RavenQueryExtensions
    {
                public static IQueryable<MessagesView> ToMessagesView(this IQueryable<MessagesViewIndex.Result> query)
                => query.OfType<ProcessedMessage>()
                    .Select(message => new
                    {
                        Id = message.UniqueMessageId,
                        MessageId = (string)message.MessageMetadata["MessageId"],
                        MessageType = (string)message.MessageMetadata["MessageType"],
                        SendingEndpoint = (EndpointDetails)message.MessageMetadata["SendingEndpoint"],
                        ReceivingEndpoint = (EndpointDetails)message.MessageMetadata["ReceivingEndpoint"],
                        TimeSent = (DateTime?)message.MessageMetadata["TimeSent"],
                        ProcessedAt = message.ProcessedAt,
                        CriticalTime = (TimeSpan)message.MessageMetadata["CriticalTime"],
                        ProcessingTime = (TimeSpan)message.MessageMetadata["ProcessingTime"],
                        DeliveryTime = (TimeSpan)message.MessageMetadata["DeliveryTime"],
                        IsSystemMessage = (bool)message.MessageMetadata["IsSystemMessage"],
                        ConversationId = (string)message.MessageMetadata["ConversationId"],
                        //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                        // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                        //Headers = message.Headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                        Headers = message.Headers.ToArray(),
                        Status = !(bool)message.MessageMetadata["IsRetried"] ? MessageStatus.Successful : MessageStatus.ResolvedSuccessfully,
                        MessageIntent = (MessageIntentEnum)message.MessageMetadata["MessageIntent"],
                        BodyUrl = (string)message.MessageMetadata["BodyUrl"],
                        BodySize = (int)message.MessageMetadata["ContentLength"],
                        InvokedSagas = (List<SagaInfo>)message.MessageMetadata["InvokedSagas"],
                        OriginatesFromSaga = (SagaInfo)message.MessageMetadata["OriginatesFromSaga"]
                    })
                    .As<MessagesView>()
                    ;


        public static IRavenQueryable<MessagesViewIndex.Result> IncludeSystemMessagesWhere(
            this IRavenQueryable<MessagesViewIndex.Result> source, HttpRequestMessage request)
        {
            var includeSystemMessages = request.GetQueryStringValue("include_system_messages", false);

            if (!includeSystemMessages)
            {
                return source.Where(m => !m.IsSystemMessage);
            }

            return source;
        }

        public static IOrderedQueryable<TSource> Paging<TSource>(this IOrderedQueryable<TSource> source, HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);
            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return (IOrderedQueryable<TSource>)source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IRavenQueryable<TSource> Paging<TSource>(this IRavenQueryable<TSource> source, HttpRequestMessage request)
        {
            var maxResultsPerPage = request.GetQueryStringValue("per_page", 50);
            if (maxResultsPerPage < 1)
            {
                maxResultsPerPage = 50;
            }

            var page = request.GetQueryStringValue("page", 1);
            if (page < 1)
            {
                page = 1;
            }

            var skipResults = (page - 1) * maxResultsPerPage;

            return source.Skip(skipResults)
                .Take(maxResultsPerPage);
        }

        public static IRavenQueryable<TSource> Sort<TSource>(this IRavenQueryable<TSource> source, HttpRequestMessage request,
            Expression<Func<TSource, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
            where TSource : MessagesViewIndex.Result
        {
            var direction = request.GetQueryStringValue("direction", defaultSortDirection);
            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }


            Expression<Func<TSource, object>> keySelector;
            var sort = request.GetQueryStringValue("sort", "time_sent");
            if (!RavenQueryableSortOptions.Contains(sort))
            {
                sort = "time_sent";
            }

            switch (sort)
            {
                case "id":
                case "message_id":
                    keySelector = m => m.MessageId;
                    break;

                case "message_type":
                    keySelector = m => m.MessageType;
                    break;

                case "critical_time":
                    keySelector = m => m.CriticalTime;
                    break;

                case "delivery_time":
                    keySelector = m => m.DeliveryTime;
                    break;

                case "processing_time":
                    keySelector = m => m.ProcessingTime;
                    break;

                case "processed_at":
                    keySelector = m => m.ProcessedAt;
                    break;

                case "status":
                    keySelector = m => m.Status;
                    break;

                default:
                    if (defaultKeySelector == null)
                    {
                        keySelector = m => m.TimeSent;
                    }
                    else
                    {
                        keySelector = defaultKeySelector;
                    }

                    break;
            }

            if (direction == "asc")
            {
                return source.OrderBy(keySelector);
            }

            return source.OrderByDescending(keySelector);
        }

        static T GetQueryStringValue<T>(this HttpRequestMessage request, string key, T defaultValue = default)
        {
            Dictionary<string, string> queryStringDictionary;
            if (!request.Properties.TryGetValue("QueryStringAsDictionary", out var dictionaryAsObject))
            {
                queryStringDictionary = request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                request.Properties["QueryStringAsDictionary"] = queryStringDictionary;
            }
            else
            {
                queryStringDictionary = (Dictionary<string, string>)dictionaryAsObject;
            }

            queryStringDictionary.TryGetValue(key, out var value);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }

        static HashSet<string> RavenQueryableSortOptions = new HashSet<string>
        {
            "processed_at",
            "id",
            "message_type",
            "time_sent",
            "critical_time",
            "delivery_time",
            "processing_time",
            "status",
            "message_id"
        };
    }
}