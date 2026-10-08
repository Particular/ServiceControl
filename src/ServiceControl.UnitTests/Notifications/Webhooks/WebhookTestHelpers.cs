namespace ServiceControl.UnitTests.Notifications.Webhooks
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Time.Testing;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Contracts.Operations;
    using ServiceControl.MessageFailures;
    using ServiceControl.MessageFailures.Api;
    using ServiceControl.Notifications.Webhooks;
    using ServiceControl.Operations;
    using ServiceControl.Persistence;
    using ServiceControl.Persistence.Infrastructure;
    using ServiceControl.Infrastructure.DomainEvents;

    static class WebhookTestData
    {
        public static readonly DateTime Now = new(2025, 3, 4, 10, 30, 0, DateTimeKind.Utc);
        public static readonly DateTime TimeOfFailure = new(2025, 3, 4, 10, 0, 0, DateTimeKind.Utc);

        public static Settings CreateSettings(string webhooksJson = "[]", string servicePulseUrl = "https://servicepulse.example.com") =>
            new()
            {
                InstanceName = "Particular.ServiceControl",
                ServicePulseUrl = servicePulseUrl,
                Webhooks = WebhookSettingsParser.Parse(webhooksJson)
            };

        public static FakeTimeProvider CreateTimeProvider() => new(new DateTimeOffset(Now));

        public static FailedMessage FailedMessage(FailedMessageStatus status, Guid? id = null)
        {
            var uniqueMessageId = (id ?? Guid.NewGuid()).ToString();
            return new FailedMessage
            {
                Id = $"FailedMessages/{uniqueMessageId}",
                UniqueMessageId = uniqueMessageId,
                Status = status,
                ProcessingAttempts =
                [
                    new FailedMessage.ProcessingAttempt
                    {
                        MessageId = "native-message-id",
                        MessageMetadata = new Dictionary<string, object>
                        {
                            ["MessageType"] = "Sales.Messages.PlaceOrder",
                            ["SendingEndpoint"] = new EndpointDetails { Name = "Sales.Api", Host = "web-01", HostId = Guid.Parse("5b8c3f4e-1111-4b55-9c25-0f3a1f6f4a01") },
                            ["ReceivingEndpoint"] = new EndpointDetails { Name = "Sales", Host = "worker-01", HostId = Guid.Parse("5b8c3f4e-2222-4b55-9c25-0f3a1f6f4a01") }
                        },
                        FailureDetails = new FailureDetails
                        {
                            AddressOfFailingEndpoint = "Sales@worker-01",
                            TimeOfFailure = TimeOfFailure,
                            Exception = new ExceptionDetails { ExceptionType = "System.InvalidOperationException", Message = "Order total is negative", StackTrace = "at Sales.PlaceOrderHandler.Handle()" }
                        }
                    }
                ]
            };
        }
    }

    sealed class StubFailedMessageStore : IFailedMessageQueryDataStore
    {
        public Dictionary<Guid, FailedMessage> Messages { get; } = [];
        public List<Guid[]> Lookups { get; } = [];
        public Exception LookupException { get; set; }

        public StubFailedMessageStore Add(params FailedMessage[] messages)
        {
            foreach (var message in messages)
            {
                Messages[Guid.Parse(message.UniqueMessageId)] = message;
            }

            return this;
        }

        public Task<FailedMessage[]> GetFailedMessagesByIds(Guid[] ids, CancellationToken cancellationToken = default)
        {
            Lookups.Add(ids);
            if (LookupException != null)
            {
                throw LookupException;
            }

            return Task.FromResult(ids.Where(Messages.ContainsKey).Select(id => Messages[id]).ToArray());
        }

        public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessages(string status, string modified, string queueAddress, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueryStatsInfo> GetFailedMessagesStats(string status, string modified, string queueAddress, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<QueryResult<IList<FailedMessageView>>> GetFailedMessagesByEndpoint(string status, string endpointName, string modified, PagingInfo pagingInfo, SortInfo sortInfo, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IDictionary<string, object>> GetFailedMessagesSummary(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FailedMessageView> GetLatestFailedMessageView(string failedMessageId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FailedMessage> GetFailedMessage(string failedMessageId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    record RecordedRequest(HttpMethod Method, Uri Uri, Dictionary<string, string> Headers, string ContentType, string Body);

    /// <summary>
    /// Records requests and replies with queued responses. Once the queue is exhausted, the last response is repeated.
    /// </summary>
    sealed class RecordingHttpHandler : HttpMessageHandler
    {
        public ConcurrentQueue<RecordedRequest> Requests { get; } = new();

        public RecordingHttpHandler RespondWith(params Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>[] responses)
        {
            foreach (var response in responses)
            {
                this.responses.Enqueue(response);
            }

            return this;
        }

        public RecordingHttpHandler RespondWith(params HttpStatusCode[] statusCodes) =>
            RespondWith([.. statusCodes.Select(code => (Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>)((_, _) => Task.FromResult(new HttpResponseMessage(code))))]);

#pragma warning disable PS0003 // HttpMessageHandler.SendAsync override signature is fixed
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers.Concat(request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase);

            Requests.Enqueue(new RecordedRequest(
                request.Method,
                request.RequestUri,
                headers,
                request.Content?.Headers.ContentType?.MediaType,
                request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            if (responses.Count > 1)
            {
                last = responses.Dequeue();
            }
            else if (responses.Count == 1)
            {
                last = responses.Peek();
            }

            return await (last ?? ((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))(request, cancellationToken);
        }
#pragma warning restore PS0003

        readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses = new();
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> last;
    }

    sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
    }

    static class WebhookSenderFactory
    {
        public static readonly WebhookDeliveryOptions FastRetries = new()
        {
            MaxRetryAttempts = 3,
            RetryDelay = TimeSpan.FromMilliseconds(1),
            MaxRetryDelay = TimeSpan.FromMilliseconds(5),
            AttemptTimeout = TimeSpan.FromMilliseconds(500)
        };

        public static WebhookSender Create(Settings settings, HttpMessageHandler handler, IDomainEvents domainEvents, WebhookDeliveryOptions options = null) =>
            new(settings, new SingleHandlerHttpClientFactory(handler), options ?? FastRetries, domainEvents, TimeProvider.System, NullLogger<WebhookSender>.Instance);
    }
}
