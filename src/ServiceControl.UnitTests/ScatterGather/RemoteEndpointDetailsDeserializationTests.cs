namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CompositeViews.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Operations;
using ServiceControl.Infrastructure;
using ServiceControl.Persistence.Infrastructure;

/// <summary>
/// Audit instances serialize their API with <c>WhenWritingNull</c>, so an endpoint whose host (or name) is not
/// known is sent without that property at all. <see cref="EndpointDetails.Host" /> and <see cref="EndpointDetails.Name" />
/// became C# <c>required</c> members in 6.20, which System.Text.Json enforces on deserialization. A single such message
/// must not make the primary drop every message the audit instance returned.
/// </summary>
[TestFixture]
class RemoteEndpointDetailsDeserializationTests
{
    const string RemoteAddress = "http://audit-1/api";

    // The shape an audit instance answers with for a message whose sending endpoint has no host,
    // e.g. one parsed from an originating address without a machine part.
    const string AuditPageWithoutSendingHost = """
        [
          {
            "id": "audit-msg-1",
            "message_id": "audit-msg-1",
            "message_type": "Sales.PlaceOrder",
            "sending_endpoint": { "name": "Sales", "host_id": "8f6b7a0e-6f0c-4c39-9a57-7d5f2e6b8a11" },
            "receiving_endpoint": { "name": "Billing", "host_id": "1d2c3b4a-0000-4c39-9a57-7d5f2e6b8a11", "host": "BILLING01" },
            "processed_at": "2026-10-07T07:00:00Z",
            "headers": []
          }
        ]
        """;

    [Test]
    public void An_endpoint_without_a_host_deserializes()
    {
        var endpoint = JsonSerializer.Deserialize<EndpointDetails>("""{ "name": "Sales", "host_id": "8f6b7a0e-6f0c-4c39-9a57-7d5f2e6b8a11" }""", SerializerOptions.Default);

        Assert.That(endpoint.Name, Is.EqualTo("Sales"));
        Assert.That(endpoint.Host, Is.Null);
    }

    [Test]
    public async Task A_remote_message_whose_endpoint_has_no_host_is_kept_and_the_result_is_complete()
    {
        var settings = new Settings { RemoteInstances = [new RemoteInstanceSetting(RemoteAddress)] };
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(AuditPageWithoutSendingHost, Encoding.UTF8, "application/json") };
            response.Headers.TryAddWithoutValidation("Total-Count", "1");
            response.Headers.TryAddWithoutValidation("ETag", "\"remote-etag\"");
            return Task.FromResult(response);
        });

        var api = new TestApi(settings, factory);

        var result = await api.Execute(new ScatterGatherApiMessageViewContext(new PagingInfo(), new SortInfo("time_sent", "desc")), "/api/messages");

        Assert.That(result.IncompleteInstances, Is.Empty, "the audit instance answered; its page must not be reported (and dropped) as a failure");
        Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(["audit-msg-1"]));
        Assert.That(result.Results.Single().SendingEndpoint.Name, Is.EqualTo("Sales"));
    }

    class TestApi(Settings settings, IHttpClientFactory factory)
        : ScatterGatherApiMessageView<object, ScatterGatherApiMessageViewContext>(new object(), settings, factory, new HttpContextAccessor(), NullLogger<TestApi>.Instance)
    {
        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewContext input, CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<IList<MessagesView>>([], new QueryStatsInfo(DataVersion.FromToken("local-etag"), 0)));
    }
}
