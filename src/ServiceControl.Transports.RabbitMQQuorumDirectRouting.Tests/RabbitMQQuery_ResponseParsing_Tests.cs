namespace ServiceControl.Transport.Tests;

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;
using Transports;
using Transports.RabbitMQ;
using System.Net.Http;
using Particular.Approvals;

[TestFixture]
class RabbitMQQuery_ResponseParsing_Tests : TransportTestFixture
{
    FakeTimeProvider provider;
    TransportSettings transportSettings;
    FakeHttpHandler httpHandler;
    RabbitMQQuery rabbitMQQuery;

    [SetUp]
    public void Initialise()
    {
        provider = new();
        provider.SetUtcNow(DateTimeOffset.UtcNow);
        transportSettings = new TransportSettings
        {
            ConnectionString = configuration.ConnectionString,
            MaxConcurrency = 1,
            EndpointName = Guid.NewGuid().ToString("N")
        };
        httpHandler = new FakeHttpHandler();
        var httpClient = new HttpClient(httpHandler) { BaseAddress = new Uri("http://localhost:15672") };

        rabbitMQQuery = new RabbitMQQuery(NullLogger<RabbitMQQuery>.Instance, provider, transportSettings, httpClient);
        rabbitMQQuery.Initialize(ReadOnlyDictionary<string, string>.Empty);
    }

    [TearDown]
    public void TearDown() => httpHandler.Dispose();

    public Func<HttpRequestMessage, HttpResponseMessage> SendCallback
    {
        get => httpHandler.SendCallback;
        set => httpHandler.SendCallback = value;
    }

    [Test]
    public async Task Should_handle_duplicated_json_data()
    {
        SendCallback = _ =>
        {
            var response = new HttpResponseMessage
            {
                Content = new StringContent("""
                    {
                        "items": [
                            {
                                "name": "queue1",
                                "vhost": "vhost1",
                                "memory": 1024,
                                "memory": 1024,
                                "message_stats": {
                                    "ack": 1
                                }
                            },
                            {
                                "name": "queue2",
                                "vhost": "vhost2",
                                "vhost": "vhost2",
                                "message_stats": {
                                    "ack": 2
                                }
                            }
                        ],
                        "page": 1,
                        "page_count": 1,
                        "page_size": 500,
                        "total_count": 2
                    }
                    """)
            };
            return response;
        };

        var queues = (await rabbitMQQuery.GetPage(1, default)).Item1;
        Approver.Verify(queues);
    }

    [Test]
    public async Task Should_fetch_queue_details_in_old_format()
    {
        SendCallback = _ =>
        {
            var response = new HttpResponseMessage
            {
                Content = new StringContent("""
                    [
                        {
                            "name": "queue1",
                            "vhost": "vhost1",
                            "memory": 1024,
                            "message_stats": {
                                "ack": 1
                            }
                        },
                        {
                            "name": "queue2",
                            "vhost": "vhost2",
                            "message_stats": {
                                "ack": 2
                            }
                        },
                        {
                            "name": "queue3",
                            "vhost": "vhost1"
                        }
                    ]
                    """)
            };
            return response;
        };

        var queues = (await rabbitMQQuery.GetPage(1, default)).Item1;
        Approver.Verify(queues);
    }

    sealed class FakeHttpHandler : HttpClientHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> SendCallback { get; set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) => SendCallback(request);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(SendCallback(request));
    }
}