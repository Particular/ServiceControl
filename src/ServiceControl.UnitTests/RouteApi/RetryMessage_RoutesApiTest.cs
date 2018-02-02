namespace ServiceControl.UnitTests.RouteApi
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Nancy;
    using Nancy.IO;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Nancy.Modules;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;

    [TestFixture]
    public class RetryMessage_RoutesApiTest
    {
        private TestApi testApi;

        private string localInstanceId;
        private string remote1InstanceId;
        private string remote2InstanceId;

        [SetUp]
        public void SetUp()
        {
            ConfigurationManager.AppSettings["ServiceControl/ForwardAuditMessages"] = bool.FalseString;
            ConfigurationManager.AppSettings["ServiceControl/ForwardErrorMessages"] = bool.FalseString;
            ConfigurationManager.AppSettings["ServiceControl/AuditRetentionPeriod"] = TimeSpan.FromHours(10).ToString();
            ConfigurationManager.AppSettings["ServiceControl/ErrorRetentionPeriod"] = TimeSpan.FromDays(10).ToString();

            testApi = new TestApi()
            {
                Settings = new Settings("TestService")
                {
                    Port = 3333,
                    RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = "http://localhost:33334/api",
                            QueueAddress = "remote1"
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
            var localResponse = new Response();
            var request = new Request("GET", "/doesntmatter", "http")
            {
                Query = new DynamicDictionary
                {
                    { "instance_id", localInstanceId }
                }
            };

            var response = await testApi.Execute(request, _ => localResponse, _ =>
            {
                throw new InvalidOperationException("should not be called");
            });

            Assert.AreSame(localResponse, response);
        }

        [Test]
        public async Task RemoteResponseReturnedWhenInstanceIdMatchesRemote()
        {
            HttpRequestMessage interceptedRequest = null;
            var request = new Request("GET", "http://doesntmatter", headers: new Dictionary<string, IEnumerable<string>>
            {
                {"SomeRequestHeader", new[] {"SomeValue"}}
            })
            {
                Query = new DynamicDictionary
                {
                    {"instance_id", remote1InstanceId}
                },
            };

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

            CollectionAssert.IsSubsetOf(new Dictionary<string, IEnumerable<string>>
            {
                {"SomeRequestHeader", new[] {"SomeValue"}}
            }, interceptedRequest.Headers);
            CollectionAssert.IsSubsetOf(new Dictionary<string, string>
            {
                {"SomeHeader", "SomeValue"},
                {"SomeContentHeader", "SomeContentValue"}
            }, response.Headers);
        }

        [Test]
        public async Task RemoteThrowsReturnsEmptyResultWithServerError()
        {
            var request = new Request("GET", "http://doesntmatter")
            {
                Query = new DynamicDictionary
                {
                    {"instance_id", remote1InstanceId}
                },
            };

            var response = await testApi.Execute(request, _ => new Response(), r =>
            {
                throw new InvalidOperationException("");
            });

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Test]
        [TestCase("POST")]
        [TestCase("PUT")]
        public async Task ContentForwardedToRemote(string method)
        {
            string interceptedStreamResult = null;
            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes("RequestContet"));
            var request = new Request(method, "http://doesntmatter", body: new RequestStream(memoryStream, 0, true))
            {
                Query = new DynamicDictionary
                {
                    {"instance_id", remote1InstanceId}
                },
            };

            var response = await testApi.Execute(request, _ => { throw new InvalidOperationException("should not be called"); }, r =>
            {
                interceptedStreamResult = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage
                {
                    Content = new StringContent("ResponseContent")
                };
            });

            var responseStream = new MemoryStream();
            response.Contents(responseStream);
            responseStream.Position = 0;
            var streamReader = new StreamReader(responseStream);

            Assert.AreEqual("RequestContet", interceptedStreamResult);
            Assert.AreEqual("ResponseContent", await streamReader.ReadToEndAsync());
        }

        class InterceptingHandler : HttpClientHandler
        {
            private Func<HttpRequestMessage, HttpResponseMessage> interceptor;

            public InterceptingHandler(Func<HttpRequestMessage, HttpResponseMessage> interceptor)
            {
                this.interceptor = interceptor;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(interceptor(request));
            }
        }

        class TestApi : RoutedApi<NoInput>
        {
            private Func<Request, Response> localResponse;

            public Task<Response> Execute(Request request, Func<Request, Response> localResponse, Func<HttpRequestMessage, HttpResponseMessage> remoteResponse)
            {
                this.localResponse = localResponse;
                HttpClientFactory = () => new HttpClient(new InterceptingHandler(remoteResponse));
                return Execute(new FakeModule { Request = request }, NoInput.Instance);
            }

            protected override Task<Response> LocalQuery(Request request, NoInput input, string instanceId)
            {
                return Task.FromResult(localResponse(request));
            }
        }

        class FakeModule : BaseModule
        {
            public override Request Request { get; set; }
        }
    }
}