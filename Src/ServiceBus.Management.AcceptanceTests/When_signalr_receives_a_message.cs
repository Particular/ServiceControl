namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Net;
    using Contexts;
    using Microsoft.AspNet.SignalR.Client;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    public class When_signalr_receives_a_message : AcceptanceTest
    {
        public class MyContext : ScenarioContext
        {
            public bool MessageReceived { get; set; }
            public string Name { get; set; }
        }

        [Test]
        public void Should_be_imported_and_accessible_via_the_rest_api()
        {
            var context = new MyContext();

            Scenario.Define(context)
                .WithEndpoint<ManagementEndpointEx>(b => b.When(_ =>
                {
                    var connection = new Connection("http://localhost:33333/api/messagestream");

                    while (true)
                    {
                        try
                        {
                            connection.Start().Wait();
                            break;
                        }
                        catch (AggregateException ex)
                        {
                            if (((HttpWebResponse) ((WebException) ex.GetBaseException()).Response).StatusCode !=
                                HttpStatusCode.NotFound)
                            {
                                break;
                            }
                        }
                    }

                    var data = @"{{ 
headers: {{""{0}"": ""{1}""}},
message: {{name: ""John""}}
}}";
                    connection.Send(String.Format(data, Headers.EnclosedMessageTypes, typeof(MyMessage).AssemblyQualifiedName));
                    connection.Stop();
                }))
                .Done(c => c.MessageReceived)
                .Run();

            Assert.AreEqual("John", context.Name);
        }

        public class ManagementEndpointEx : ManagementEndpoint
        {
            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public void Handle(MyMessage message)
                {
                    Context.MessageReceived = true;
                    Context.Name = message.Name;
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public string Name { get; set; }
        }
    }
}