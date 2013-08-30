namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Globalization;
    using System.Net;
    using Contexts;
    using Microsoft.AspNet.SignalR.Client;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.Infrastructure.SignalR;

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

                    var serializerSettings = new JsonSerializerSettings
                    {
                        ContractResolver = new CustomSignalRContractResolverBecauseOfIssue500InSignalR(),
                        Formatting = Formatting.None,
                        NullValueHandling = NullValueHandling.Ignore,
                        Converters = { new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.RoundtripKind } }
                    };
                    
                    connection.JsonSerializer = JsonSerializer.Create(serializerSettings);

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

                    var data = @"{ 
type: 'MyRequest',
message: {name: 'John'}
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
            public class MyMessageHandler : IHandleMessages<MyRequest>
            {
                public MyContext Context { get; set; }

                public void Handle(MyRequest message)
                {
                    Context.MessageReceived = true;
                    Context.Name = message.Name;
                }
            }
        }

        [Serializable]
        public class MyRequest : ICommand
        {
            public string Name { get; set; }
        }
    }
}