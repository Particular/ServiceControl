namespace ServiceControl.UnitTests.RouteApi
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure.Settings;

    [TestFixture]
    public class RetryMessage_RoutesApiTest
    {
        [SetUp]
        public void SetUp()
        {
            settings = new Settings("TestService")
            {
                Port = 3333,
                RemoteInstances = new[] { new RemoteInstanceSetting { ApiUri = "http://localhost:33334/api" } }
            };

            localInstanceId = InstanceIdGenerator.FromApiUrl(settings.ApiUrl);
            remote1InstanceId = InstanceIdGenerator.FromApiUrl(settings.RemoteInstances[0].ApiUri);
        }

        [Test]
        public async Task LocalResponseReturnedWhenInstanceIdMatchesLocal()
        {
            var localResponse = new HttpResponseMessage();

            var testApi = new TestApi(settings, new DefaultHttpContext(), _ => localResponse,
                _ => throw new InvalidOperationException("should not be called"));

            var response = await testApi.Execute(new(localInstanceId));

            Assert.AreSame(localResponse, response);
        }

        [Test]
        public async Task RemoteResponseReturnedWhenInstanceIdMatchesRemote()
        {
            HttpRequestMessage interceptedRequest = null;

            var defaultHttpContext = new DefaultHttpContext { Request = { Method = "GET" } };
            defaultHttpContext.Request.Headers.Append("SomeRequestHeader", "SomeValue");

            var testApi = new TestApi(settings, defaultHttpContext,
                _ => throw new InvalidOperationException("should not be called"),
                r =>
                {
                    interceptedRequest = r;
                    return new HttpResponseMessage
                    {
                        Headers = { { "SomeHeader", "SomeValue" } },
                        Content = new StringContent("")
                        {
                            Headers = { { "SomeContentHeader", "SomeContentValue" } }
                        }
                    };
                });

            var response = await testApi.Execute(new(remote1InstanceId));

            CollectionAssert.IsSubsetOf(new[] { "SomeValue" },
                interceptedRequest.Headers.GetValues("SomeRequestHeader"));
            CollectionAssert.IsSubsetOf(new[] { "SomeValue" }, response.Headers.GetValues("SomeHeader"));
            CollectionAssert.IsSubsetOf(new[] { "SomeContentValue" },
                response.Content.Headers.GetValues("SomeContentHeader"));
        }

        [Test]
        public async Task RemoteThrowsReturnsEmptyResultWithServerError()
        {
            var testApi = new TestApi(settings,
                new DefaultHttpContext { Request = { Method = "GET" } }, _ => default,
                r => throw new InvalidOperationException(""));

            var response = await testApi.Execute(new(remote1InstanceId));

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Test]
        [TestCase("POST")]
        [TestCase("PUT")]
        public async Task ContentForwardedToRemote(string method)
        {
            string interceptedStreamResult = null;

            var defaultHttpContext = new DefaultHttpContext { Request = { Method = method } };
            defaultHttpContext.Request.Headers.ContentType = "application/octet-stream";
            defaultHttpContext.Request.Body = new MemoryStream("RequestContent"u8.ToArray());

            var testApi = new TestApi(settings, defaultHttpContext, _ => default,
                r =>
                {
                    interceptedStreamResult = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    return new HttpResponseMessage { Content = new StringContent("ResponseContent") };
                });

            var response = await testApi.Execute(new(remote1InstanceId));

            Assert.AreEqual("RequestContent", interceptedStreamResult);
            Assert.AreEqual("ResponseContent", await response.Content.ReadAsStringAsync());
        }

        string localInstanceId;
        string remote1InstanceId;
        Settings settings;

        class InterceptingHandler(Func<HttpRequestMessage, HttpResponseMessage> interceptor) : HttpClientHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(interceptor(request));
        }

        class TestApi : RoutedApi<TestApiContext>
        {
            readonly Func<TestApiContext, HttpResponseMessage> localResponse;

            public TestApi(Settings settings, HttpContext httpContext,
                Func<TestApiContext, HttpResponseMessage> localResponse,
                Func<HttpRequestMessage, HttpResponseMessage> remoteResponse) : base(settings,
                new HttpClientFakeFactory(remoteResponse), new HttpContextAccessorFake(httpContext)) =>
                this.localResponse = localResponse;

            protected override Task<HttpResponseMessage> LocalQuery(TestApiContext testApiContext) =>
                Task.FromResult(localResponse(testApiContext));

            class HttpClientFakeFactory(Func<HttpRequestMessage, HttpResponseMessage> response)
                : IHttpClientFactory
            {
                public HttpClient CreateClient(string name) => new(new InterceptingHandler(response)) { BaseAddress = new Uri("http://doesntmatter") };
            }

            class HttpContextAccessorFake(HttpContext httpContext) : IHttpContextAccessor
            {
                public HttpContext HttpContext { get; set; } = httpContext;
            }
        }

        public record TestApiContext(string InstanceId) : RoutedApiContext(InstanceId);
    }
}