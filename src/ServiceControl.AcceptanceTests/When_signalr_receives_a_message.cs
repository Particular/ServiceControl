namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Net;
    using System.Threading;
    using Contexts;
    using Microsoft.AspNet.SignalR.Client;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.SignalR;
    using ServiceControl.MessageFailures.InternalMessages;
    using JsonSerializer = Newtonsoft.Json.JsonSerializer;

    public class When_signalr_receives_a_message : AcceptanceTest
    {
        public class MyContext : ScenarioContext
        {
            public bool MessageReceived { get; set; }
            public string Name { get; set; }
        }

        [Test, Ignore("Since servicecontrol now only loads ServiceControl.dll this tests needs to be rewritten since the endpoint wont load the types in this file")]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Define(context)
                .WithEndpoint<ManagementEndpointEx>(b => b.AppConfig(PathToAppConfig)
                    .When(_ =>
                    {
                        var connection = new Connection("http://localhost:33333/api/messagestream")
                        {
                            JsonSerializer = JsonSerializer.Create(SerializationSettingsFactoryForSignalR.CreateDefault())
                        };

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
                                var statusCode = ((HttpWebResponse) webException.Response).StatusCode;
                                if (statusCode != HttpStatusCode.NotFound && statusCode != HttpStatusCode.ServiceUnavailable)
                                {
                                    break;
                                }

                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }

                        var data = @"{ 
type: 'ArchiveMessage',
message: {failed_message_id: 'John'}
}";
                        connection.Send(data);
                        connection.Stop();
                    }))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.AreEqual("John", context.Name);
        }

        public class ManagementEndpointEx : ManagementEndpoint
        {
            public class MyMessageHandler : IHandleMessages<ArchiveMessage>
            {
                public MyContext Context { get; set; }

                public void Handle(ArchiveMessage message)
                {
                    Context.Name = message.FailedMessageId;
                    Context.MessageReceived = true;
                }
            }
        }
    }
}