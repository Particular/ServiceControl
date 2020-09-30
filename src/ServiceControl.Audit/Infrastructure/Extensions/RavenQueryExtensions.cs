namespace ServiceControl.Infrastructure.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.Http;
    using Audit.Auditing;
    using Audit.Auditing.MessagesView;
    using Raven.Client.Documents;
    using Raven.Client.Documents.Linq;

    static class RavenQueryExtensions
    {
        public static IQueryable<MessagesView> ToMessagesView(this IQueryable<MessagesViewIndex.Result> query)
            => query.OfType<ProcessedMessage>()
                .Select(message => new
                {
                    Id = message.UniqueMessageId,
                    MessageId = message.MessageId,
                    MessageType = message.MessageType,
                    SendingEndpoint = message.SendingEndpoint,
                    ReceivingEndpoint = message.ReceivingEndpoint,
                    TimeSent = message.TimeSent,
                    ProcessedAt = message.ProcessedAt,
                    CriticalTime = message.CriticalTime,
                    ProcessingTime = message.ProcessingTime,
                    DeliveryTime = message.DeliveryTime,
                    IsSystemMessage = message.IsSystemMessage,
                    ConversationId = message.ConversationId,
                    //the reason the we need to use a KeyValuePair<string, object> is that raven seems to interpret the values and convert them
                    // to real types. In this case it was the NServiceBus.Temporary.DelayDeliveryWith header to was converted to a timespan
                    //Headers = message.Headers.Select(header => new KeyValuePair<string, object>(header.Key, header.Value)),
                    Headers = message.Headers.ToArray(),
                    Status = message.Status,
                    MessageIntent = message.MessageIntent,
                    BodyUrl = message.BodyUrl,
                    BodySize = message.BodySize,
                    InvokedSagas = message.InvokedSagas,
                    OriginatesFromSaga = message.OriginatesFromSaga
                })
                .As<MessagesView>();

        public static IQueryable<MessagesView> ToMessagesView(this IQueryable<ProcessedMessage> query)
            => query.Select(x => new MessagesView
            {
                ProcessingTime = x.ProcessingTime,
                SendingEndpoint = x.SendingEndpoint,
                ReceivingEndpoint = x.ReceivingEndpoint,
                MessageType = x.MessageType,
                MessageId = x.MessageId,
                ConversationId = x.ConversationId,
                DeliveryTime = x.DeliveryTime,
                TimeSent = x.TimeSent,
                CriticalTime = x.CriticalTime,
                IsSystemMessage = x.IsSystemMessage,
                Status = x.Status,
                ProcessedAt = x.ProcessedAt,
                Headers = x.Headers.ToArray(),
                MessageIntent = x.MessageIntent,
                InvokedSagas = x.InvokedSagas,
                OriginatesFromSaga = x.OriginatesFromSaga,
                BodyUrl = x.BodyUrl,
                BodySize = x.BodySize,
                Id = x.Id
            });


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

        public static IRavenQueryable<ProcessedMessage> IncludeSystemMessagesWhere(
            this IRavenQueryable<ProcessedMessage> source, HttpRequestMessage request)
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

        public static IRavenQueryable<MessagesViewIndex.Result> Sort(this IRavenQueryable<MessagesViewIndex.Result> source, HttpRequestMessage request,
            Expression<Func<MessagesViewIndex.Result, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
        {
            var direction = request.GetQueryStringValue("direction", defaultSortDirection);
            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }


            Expression<Func<MessagesViewIndex.Result, object>> keySelector;
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

        public static IRavenQueryable<ProcessedMessage> Sort(this IRavenQueryable<ProcessedMessage> source, HttpRequestMessage request,
            Expression<Func<ProcessedMessage, object>> defaultKeySelector = null, string defaultSortDirection = "desc")
        {
            var direction = request.GetQueryStringValue("direction", defaultSortDirection);
            if (direction != "asc" && direction != "desc")
            {
                direction = defaultSortDirection;
            }


            Expression<Func<ProcessedMessage, object>> keySelector;
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