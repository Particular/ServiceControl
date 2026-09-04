#pragma warning disable PS0003 // Make the CancellationToken parameter optional — HttpMessageHandler.SendAsync override signature is fixed

namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CompositeViews.MessageCounting;
using CompositeViews.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Api.Contracts;
using ServiceControl.Infrastructure.Api;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Persistence.Infrastructure;

/// <summary>
/// A timed-out or failed instance is not an instance with no data: the composite keeps what the others
/// returned and says which instances are missing, and only gives up when nothing answered.
/// </summary>
[TestFixture]
class IncompleteResultsTests
{
    const string RemoteAddress = "http://audit-1/api";
    const string OtherRemoteAddress = "http://audit-2/api";

    [Test]
    public async Task A_local_query_timeout_keeps_the_remote_data_and_reports_the_local_instance()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Healthy("remote-msg"));

        var api = new TestApi(settings, factory, _ => throw new TimeoutException("query time limit"));

        var result = await api.Execute(Context(), "/api/messages");

        Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(["remote-msg"]));
        Assert.That(result.IncompleteInstances, Is.EqualTo([new IncompleteInstance(settings.InstanceId, QueryFailure.TimedOut)]));
    }

    [Test]
    public async Task A_remote_query_timeout_keeps_the_local_data_and_reports_the_remote_instance()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Status(HttpStatusCode.GatewayTimeout));

        var api = new TestApi(settings, factory, Local("local-msg"));

        var result = await api.Execute(Context(), "/api/messages");

        Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(["local-msg"]));
        Assert.That(result.IncompleteInstances, Is.EqualTo([new IncompleteInstance(settings.RemoteInstances[0].InstanceId, QueryFailure.TimedOut)]));
        Assert.That(settings.RemoteInstances[0].TemporarilyUnavailable, Is.False, "a remote whose query timed out is up; it must not be skipped on the next query");
    }

    [Test]
    public async Task A_remote_error_is_reported_as_a_failure_not_as_no_data()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Status(HttpStatusCode.InternalServerError));

        var api = new TestApi(settings, factory, Local("local-msg"));

        var result = await api.Execute(Context(), "/api/messages");

        Assert.That(result.IncompleteInstances, Is.EqualTo([new IncompleteInstance(settings.RemoteInstances[0].InstanceId, QueryFailure.Failed)]));
    }

    [Test]
    public async Task A_temporarily_unavailable_remote_is_reported_as_missing()
    {
        var settings = Settings(RemoteAddress);
        settings.RemoteInstances[0].TemporarilyUnavailable = true;

        var api = new TestApi(settings, new FakeHttpClientFactory(), Local("local-msg"));

        var result = await api.Execute(Context(), "/api/messages");

        Assert.That(result.Results.Select(m => m.MessageId), Is.EqualTo(["local-msg"]));
        Assert.That(result.IncompleteInstances, Is.EqualTo([new IncompleteInstance(settings.RemoteInstances[0].InstanceId, QueryFailure.Unavailable)]));
    }

    [Test]
    public async Task An_incomplete_result_carries_no_version()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Status(HttpStatusCode.GatewayTimeout));

        var result = await new TestApi(settings, factory, Local("local-msg")).Execute(Context(), "/api/messages");

        Assert.That(result.QueryStats.Version.HasValue, Is.False, "a client must not cache an incomplete page as if it were the whole answer");
    }

    [Test]
    public async Task A_complete_result_reports_nothing_missing()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Healthy("remote-msg"));

        var result = await new TestApi(settings, factory, Local("local-msg")).Execute(Context(), "/api/messages");

        Assert.That(result.IncompleteInstances, Is.Empty);
        Assert.That(result.Results, Has.Count.EqualTo(2));
    }

    [Test]
    public void When_no_instance_answered_and_one_timed_out_the_query_is_a_timeout()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Status(HttpStatusCode.GatewayTimeout));

        var api = new TestApi(settings, factory, _ => throw new TimeoutException("query time limit"));

        var exception = Assert.ThrowsAsync<TimeoutException>(() => api.Execute(Context(), "/api/messages"));

        Assert.That(exception.Message, Does.Contain(settings.RemoteInstances[0].InstanceId));
    }

    [Test]
    public void A_remote_only_query_whose_only_remote_timed_out_is_a_timeout()
    {
        var settings = Settings(RemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Status(HttpStatusCode.GatewayTimeout));

        var api = new GetAuditCountsForEndpointApi(settings, factory, new HttpContextAccessor(), NullLogger<GetAuditCountsForEndpointApi>.Instance);

        Assert.ThrowsAsync<TimeoutException>(() => api.Execute(new AuditCountsForEndpointContext(new PagingInfo(), "Sales"), "/api/endpoints/Sales/audit-count"));
    }

    [Test]
    public void Audit_counts_missing_an_instance_are_not_recorded_as_the_endpoint_throughput()
    {
        var settings = Settings(RemoteAddress, OtherRemoteAddress);
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Json<IList<AuditCount>>([new AuditCount { UtcDate = DateTime.UtcNow.Date, Count = 5 }]));
        factory.Register(settings.RemoteInstances[1], Status(HttpStatusCode.GatewayTimeout));

        var auditCountApi = new AuditCountApi(new GetAuditCountsForEndpointApi(settings, factory, new HttpContextAccessor(), NullLogger<GetAuditCountsForEndpointApi>.Instance));

        var exception = Assert.CatchAsync(() => auditCountApi.GetEndpointAuditCounts("Sales"));

        Assert.That(exception.Message, Does.Contain(settings.RemoteInstances[1].InstanceId), "a partial sum recorded as the day's throughput would under-count the license usage for good");
    }

    [Test]
    public void The_response_names_the_missing_instances_in_a_header()
    {
        var context = new DefaultHttpContext();
        var result = new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero)
        {
            IncompleteInstances = [new IncompleteInstance("audit-1", QueryFailure.TimedOut), new IncompleteInstance("audit-2", QueryFailure.Unavailable)]
        };

        context.Response.WithScatterGatherResult(result, new PagingInfo());

        Assert.That(context.Response.Headers[HttpResponseExtensions.IncompleteResultsHeader].ToString(), Is.EqualTo("audit-1:timeout, audit-2:unavailable"));
    }

    [Test]
    public void A_complete_response_has_no_incomplete_results_header()
    {
        var context = new DefaultHttpContext();

        context.Response.WithScatterGatherResult(new QueryResult<IList<MessagesView>>([], QueryStatsInfo.Zero), new PagingInfo());

        Assert.That(context.Response.Headers.ContainsKey(HttpResponseExtensions.IncompleteResultsHeader), Is.False);
    }

    static Settings Settings(params string[] remotes) => new()
    {
        RemoteInstances = remotes.Select(address => new RemoteInstanceSetting(address)).ToArray()
    };

    static ScatterGatherApiMessageViewContext Context() => new(new PagingInfo(), new SortInfo("time_sent", "desc"));

    static Func<CancellationToken, Task<QueryResult<IList<MessagesView>>>> Local(string messageId) =>
        _ => Task.FromResult(new QueryResult<IList<MessagesView>>([new MessagesView { MessageId = messageId }], new QueryStatsInfo(DataVersion.FromToken("local-etag"), 1)));

    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Healthy(string messageId) =>
        Json<IList<MessagesView>>([new MessagesView { MessageId = messageId }]);

    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Json<T>(T body) =>
        (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body, SerializerOptions.Default)) };
            response.Headers.TryAddWithoutValidation("Total-Count", "1");
            response.Headers.TryAddWithoutValidation("ETag", "\"remote-etag\"");
            return Task.FromResult(response);
        };

    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Status(HttpStatusCode statusCode) =>
        (_, _) => Task.FromResult(new HttpResponseMessage(statusCode));

    class TestApi(Settings settings, IHttpClientFactory factory, Func<CancellationToken, Task<QueryResult<IList<MessagesView>>>> local)
        : ScatterGatherApiMessageView<object, ScatterGatherApiMessageViewContext>(new object(), settings, factory, new HttpContextAccessor(), NullLogger<TestApi>.Instance)
    {
        protected override Task<QueryResult<IList<MessagesView>>> LocalQuery(ScatterGatherApiMessageViewContext input, CancellationToken cancellationToken = default) => local(cancellationToken);
    }

    class FakeHttpClientFactory : IHttpClientFactory
    {
        readonly ConcurrentDictionary<string, (HttpMessageHandler Handler, string BaseAddress)> handlers = new();

        public void Register(RemoteInstanceSetting remote, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) =>
            handlers[remote.InstanceId] = (new StubHandler(responder), remote.BaseAddress);

        public HttpClient CreateClient(string name)
        {
            var (handler, baseAddress) = handlers[name];
            return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri(baseAddress) };
        }
    }

    class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
