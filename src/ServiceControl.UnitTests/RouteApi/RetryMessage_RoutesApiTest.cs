namespace ServiceControl.UnitTests.RouteApi
{
    using System;
    using System.Configuration;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web.Http;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    [TestFixture]
    public class RetryMessage_RoutesApiTest
    {
        [SetUp]
        public void SetUp()
        {
            ConfigurationManager.AppSettings["ServiceControl/ForwardErrorMessages"] = bool.FalseString;
            ConfigurationManager.AppSettings["ServiceControl/ErrorRetentionPeriod"] = TimeSpan.FromDays(10).ToString();

            testApi = new TestApi
            {
                Settings = new Settings("TestService")
                {
                    Port = 3333,
                    RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = "http://localhost:33334/api"
                        }
                    }
                }
            };

            localInstanceId = InstanceIdGenerator.FromApiUrl(testApi.Settings.ApiUrl);
            remote1InstanceId = InstanceIdGenerator.FromApiUrl(testApi.Settings.RemoteInstances[0].ApiUri);
        }

        [Test]
        public async Task LocalResponseReturnedWhenInstanceIdMatchesLocal()
        {
            var localResponse = new HttpResponseMessage();
            var request = new HttpRequestMessage(new HttpMethod("GET"), $"http://doesntmatter?instance_id={localInstanceId}");

            var response = await testApi.Execute(request, _ => localResponse, _ => { throw new InvalidOperationException("should not be called"); });

            Assert.AreSame(localResponse, response);
        }

        [Test]
        public async Task RemoteResponseReturnedWhenInstanceIdMatchesRemote()
        {
            HttpRequestMessage interceptedRequest = null;

            var request = new HttpRequestMessage(new HttpMethod("GET"), $"http://doesntmatter?instance_id={remote1InstanceId}");
            request.Headers.Add("SomeRequestHeader", "SomeValue");

            var response = await testApi.Execute(request, _ => { throw new InvalidOperationException("should not be called"); }, r =>
            {
                interceptedRequest = r;
                return new HttpResponseMessage
                {
                    Headers =
                    {
                        {"SomeHeader", "SomeValue"}
                    },
                    Content = new StringContent("")
                    {
                        Headers =
                        {
                            {"SomeContentHeader", "SomeContentValue"}
                        }
                    }
                };
            });


            CollectionAssert.IsSubsetOf(new[]
            {
                "SomeValue"
            }, interceptedRequest.Headers.GetValues("SomeRequestHeader"));
            CollectionAssert.IsSubsetOf(new[]
            {
                "SomeValue"
            }, response.Headers.GetValues("SomeHeader"));
            CollectionAssert.IsSubsetOf(new[]
            {
                "SomeContentValue"
            }, response.Content.Headers.GetValues("SomeContentHeader"));
        }

        [Test]
        public async Task RemoteThrowsReturnsEmptyResultWithServerError()
        {
            var request = new HttpRequestMessage(new HttpMethod("GET"), $"http://doesntmatter?instance_id={remote1InstanceId}");

            var response = await testApi.Execute(request, _ => new HttpResponseMessage(), r => { throw new InvalidOperationException(""); });

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Test]
        [TestCase("POST")]
        [TestCase("PUT")]
        public async Task ContentForwardedToRemote(string method)
        {
            string interceptedStreamResult = null;
            var request = new HttpRequestMessage(new HttpMethod(method), $"http://doesntmatter?instance_id={remote1InstanceId}");

            var binaryContent = new ByteArrayContent(Encoding.UTF8.GetBytes("RequestContet"));
            binaryContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = binaryContent;

            var response = await testApi.Execute(request, _ => { throw new InvalidOperationException("should not be called"); }, r =>
            {
                interceptedStreamResult = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage
                {
                    Content = new StringContent("ResponseContent")
                };
            });

            Assert.AreEqual("RequestContet", interceptedStreamResult);
            Assert.AreEqual("ResponseContent", await response.Content.ReadAsStringAsync());
        }

        TestApi testApi;

        string localInstanceId;
        string remote1InstanceId;

        class InterceptingHandler : HttpClientHandler
        {
            public InterceptingHandler(Func<HttpRequestMessage, HttpResponseMessage> interceptor)
            {
                this.interceptor = interceptor;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(interceptor(request));
            }

            Func<HttpRequestMessage, HttpResponseMessage> interceptor;
        }

        class TestApi : RoutedApi<NoInput>
        {
            public Task<HttpResponseMessage> Execute(HttpRequestMessage request, Func<HttpRequestMessage, HttpResponseMessage> localResponse, Func<HttpRequestMessage, HttpResponseMessage> remoteResponse)
            {
                this.localResponse = localResponse;
                HttpClientFactory = () => new HttpClient(new InterceptingHandler(remoteResponse));
                return Execute(new FakeController
                {
                    Request = request
                }, NoInput.Instance);
            }

            protected override Task<HttpResponseMessage> LocalQuery(HttpRequestMessage request, NoInput input, string instanceId)
            {
                return Task.FromResult(localResponse(request));
            }

            Func<HttpRequestMessage, HttpResponseMessage> localResponse;
        }

        class FakeController : ApiController
        {
        }
    }
}