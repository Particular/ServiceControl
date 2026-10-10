#pragma warning disable PS0003 // Make the CancellationToken parameter optional — HttpMessageHandler.SendAsync override signature is fixed

namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ServiceBus.Management.Infrastructure.Settings;

/// <summary>
/// Hands out a client per remote instance whose answers are scripted, with the timeout the real
/// remote clients get from <see cref="ServiceControl.Infrastructure.WebApi.RemoteInstanceServiceCollectionExtensions"/>: the query time limit.
/// </summary>
class FakeHttpClientFactory : IHttpClientFactory
{
    readonly ConcurrentDictionary<string, (HttpMessageHandler Handler, string BaseAddress, TimeSpan Timeout)> handlers = new();

    public void Register(RemoteInstanceSetting remote, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder, TimeSpan? queryTimeLimit = null) =>
        handlers[remote.InstanceId] = (new StubHandler(responder), remote.BaseAddress, queryTimeLimit ?? TimeSpan.FromSeconds(100));

    public HttpClient CreateClient(string name)
    {
        var (handler, baseAddress, timeout) = handlers[name];
        return new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri(baseAddress), Timeout = timeout };
    }

    class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
