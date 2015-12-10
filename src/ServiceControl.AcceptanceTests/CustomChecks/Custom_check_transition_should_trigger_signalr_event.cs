namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Net;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.Plugin.CustomChecks;
    using Microsoft.AspNet.SignalR.Client;
    [TestFixture]
    public class Custom_check_transition_should_trigger_signalr_event : AcceptanceTest
    {
        [Test]
        public void Should_result_in_a_custom_check_failed_event()
        {
            var context = new MyContext
            {
                SCPort = port
            };

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run();

            Assert.True(context.SignalrData.IndexOf("\"severity\": \"error\",") > 0);
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public int SCPort { get; set; }
            public string SignalrData { get; set; }
        }

        public class EndpointThatUsesSignalR : EndpointConfigurationBuilder
        {
            public EndpointThatUsesSignalR()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            class SignalrStarter : IWantToRunWhenBusStartsAndStops
            {
                private readonly MyContext context;
                Connection connection;

                public SignalrStarter(MyContext context)
                {
                    this.context = context;
                    connection = new Connection(string.Format("http://localhost:{0}/api/messagestream", context.SCPort));
                }

                public void Start()
                {
                    var jsonSerializerSettings = SerializationSettingsFactoryForSignalR.CreateDefault();
                    jsonSerializerSettings.Converters.Clear();
                    connection.JsonSerializer = Newtonsoft.Json.JsonSerializer.Create(jsonSerializerSettings);
                    connection.Received += ConnectionOnReceived;

                    while (true)
                    {
                        try
                        {
                            connection.Start().Wait();
                            break;
                        }
                        catch (AggregateException ex)
                        {
                            var exception = ex.GetBaseException();
                            var webException = exception as WebException;

                            if (webException == null)
                            {
                                continue;
                            }
                            var statusCode = ((HttpWebResponse)webException.Response).StatusCode;
                            if (statusCode != HttpStatusCode.NotFound && statusCode != HttpStatusCode.ServiceUnavailable)
                            {
                                break;
                            }
                        }
                    }
                }

                private void ConnectionOnReceived(string s)
                {
                    if (s.IndexOf("\"EventLogItemAdded\"") > 0)
                    {
                        if (s.IndexOf("EventLogItem/CustomChecks/CustomCheckFailed") > 0)
                        {
                            context.SignalrData = s;
                            context.SignalrEventReceived = true;
                        }
                    }
                }

                public void Stop()
                {
                    connection.Stop();
                }
            }
        }
        public class EndpointWithFailingCustomCheck : EndpointConfigurationBuilder
        {

            public EndpointWithFailingCustomCheck()
            {
                EndpointSetup<DefaultServerWithoutAudit>();
            }

            public class EventuallyFailingCustomCheck : PeriodicCheck
            {
                private static int counter;

                public EventuallyFailingCustomCheck()
                    : base("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1)) { }

                public override CheckResult PerformCheck()
                {
                    if ((Interlocked.Increment(ref counter) / 10) % 2 == 0)
                    {
                        return CheckResult.Failed("fail!");
                    }
                    return CheckResult.Pass;
                }
            }
        }
    }
}
