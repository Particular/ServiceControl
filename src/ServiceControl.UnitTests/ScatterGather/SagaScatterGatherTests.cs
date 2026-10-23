namespace ServiceControl.UnitTests.ScatterGather;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using ServiceBus.Management.Infrastructure.Settings;
using ServiceControl.Infrastructure.WebApi;
using ServiceControl.Persistence.Infrastructure;
using ServiceControl.SagaAudit;

/// <summary>
/// A saga usually lives on one audit instance, so every other instance answers that it has none.
/// </summary>
[TestFixture]
class SagaScatterGatherTests
{
    [Test]
    public async Task An_instance_without_the_saga_answers_rather_than_fails()
    {
        var sagaId = Guid.NewGuid();
        var settings = Settings("http://audit-1/api", "http://audit-2/api");
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Found(sagaId, "\"has-it\""));
        factory.Register(settings.RemoteInstances[1], NotFound("\"has-none\""));

        var api = new GetSagaByIdApi(settings, factory, new HttpContextAccessor(), NullLogger<GetSagaByIdApi>.Instance);

        var result = await api.Execute(new SagaByIdContext(new PagingInfo(), sagaId), $"/api/sagas/{sagaId}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results?.SagaId, Is.EqualTo(sagaId));
            Assert.That(result.IncompleteInstances, Is.Empty, "an instance that has no such saga still answered");
            Assert.That(result.QueryStats.Version.HasValue, Is.True, "every instance sent a version, so the composite has one to send");
        }
    }

    [Test]
    public async Task An_older_instance_without_the_saga_answers_but_leaves_no_version()
    {
        var sagaId = Guid.NewGuid();
        var settings = Settings("http://audit-1/api", "http://audit-2/api");
        var factory = new FakeHttpClientFactory();
        factory.Register(settings.RemoteInstances[0], Found(sagaId, "\"has-it\""));
        factory.Register(settings.RemoteInstances[1], NotFound(etag: null));

        var api = new GetSagaByIdApi(settings, factory, new HttpContextAccessor(), NullLogger<GetSagaByIdApi>.Instance);

        var result = await api.Execute(new SagaByIdContext(new PagingInfo(), sagaId), $"/api/sagas/{sagaId}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Results?.SagaId, Is.EqualTo(sagaId));
            Assert.That(result.IncompleteInstances, Is.Empty, "an instance from before the fix still answered");
            Assert.That(result.QueryStats.Version.HasValue, Is.False, "one instance offered no version, so the composite cannot promise anything");
        }
    }

    static Settings Settings(params string[] remotes) => new()
    {
        RemoteInstances = Array.ConvertAll(remotes, address => new RemoteInstanceSetting(address))
    };

    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Found(Guid sagaId, string etag) =>
        (_, _) =>
        {
            var history = new SagaHistory { Id = sagaId, SagaId = sagaId, SagaType = "MySaga", Changes = [] };
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(history, SerializerOptions.Default)) };
            response.Headers.TryAddWithoutValidation("ETag", etag);
            return Task.FromResult(response);
        };

    // What an audit instance sends for a missing saga: ASP.NET Core turns the null result into a 204.
    static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> NotFound(string etag) =>
        (_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.NoContent);
            if (etag != null)
            {
                response.Headers.TryAddWithoutValidation("ETag", etag);
            }

            return Task.FromResult(response);
        };
}
