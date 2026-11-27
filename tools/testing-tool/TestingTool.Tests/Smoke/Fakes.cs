using System.Net;
using System.Text;
using NServiceBus;

namespace TestingTool.Tests.Smoke;

sealed class FakeMessageSession : IMessageSession
{
    public List<(object Message, SendOptions Options)> Sent { get; } = [];
    public List<Type> Subscribed { get; } = [];
    public List<Type> Unsubscribed { get; } = [];

    public Task Send(object message, SendOptions sendOptions, CancellationToken cancellationToken = default)
    {
        Sent.Add((message, sendOptions));
        return Task.CompletedTask;
    }

    public Task Subscribe(Type eventType, SubscribeOptions subscribeOptions, CancellationToken cancellationToken = default)
    {
        Subscribed.Add(eventType);
        return Task.CompletedTask;
    }

    public Task Unsubscribe(Type eventType, UnsubscribeOptions unsubscribeOptions, CancellationToken cancellationToken = default)
    {
        Unsubscribed.Add(eventType);
        return Task.CompletedTask;
    }

    public Task Send<T>(Action<T> messageConstructor, SendOptions sendOptions, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task Publish(object message, PublishOptions publishOptions, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task Publish<T>(Action<T> messageConstructor, PublishOptions publishOptions, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<(HttpMethod Method, string PathAndQuery, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));
        return respond(request);
    }

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);
}
