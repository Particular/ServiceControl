namespace ServiceBus.Management.AcceptanceTests.CustomChecks
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.Plugin.CustomChecks;
    using Microsoft.AspNet.SignalR.Client;
    using Microsoft.AspNet.SignalR.Client.Transports;

    [TestFixture]
    public class Custom_check_transition_should_trigger_signalr_event : AcceptanceTest
    {
        [Test]
        public void Should_result_in_a_custom_check_failed_event()
        {
            var context = new MyContext
            {
                Handler = () => Handler
            };

            Define(context)
                .WithEndpoint<EndpointWithFailingCustomCheck>()
                .WithEndpoint<EndpointThatUsesSignalR>()
                .Done(c => c.SignalrEventReceived)
                .Run(TimeSpan.FromMinutes(2));

            Assert.True(context.SignalrData.IndexOf("\"severity\": \"error\",") > 0);
        }

        public class MyContext : ScenarioContext
        {
            public bool SignalrEventReceived { get; set; }
            public Func<HttpMessageHandler> Handler { get; set; }
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
                    connection = new Connection("http://localhost/api/messagestream")
                    {
                        JsonSerializer = Newtonsoft.Json.JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault())
                    };
                }

                public void Start()
                {
                    connection.Received += ConnectionOnReceived;

                    connection.Start(new ServerSentEventsTransport(new SignalRHttpClient(context.Handler()))).GetAwaiter().GetResult();
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
                EndpointSetup<DefaultServerWithoutAudit>().IncludeAssembly(typeof(PeriodicCheck).Assembly);
            }

            public class EventuallyFailingCustomCheck : PeriodicCheck
            {
                private static int counter;

                public EventuallyFailingCustomCheck()
                    : base("EventuallyFailingCustomCheck", "Testing", TimeSpan.FromSeconds(1)) { }

                public override CheckResult PerformCheck()
                {
                    if ((Interlocked.Increment(ref counter) / 5) % 2 == 1)
                    {
                        return CheckResult.Failed("fail!");
                    }
                    return CheckResult.Pass;
                }
            }
        }
    }
}
