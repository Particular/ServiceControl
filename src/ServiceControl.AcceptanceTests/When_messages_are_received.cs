namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Endpoints;

    public class When_messages_are_received : AcceptanceTest
    {
        public class MyContext : ScenarioContext
        {
            public string EndpointValid_Name { get; set; }
            public string EndpointExpired_Name { get; set; }
            public string EndpointNoLicense_Name { get; set; }
        }

        [Test]
        public void License_status_should_be_exposed()
        {
            var context = new MyContext();
            var endpoints = new List<EndpointsView>();

            Scenario.Define(context)
            .WithEndpoint<ManagementEndpoint>(c => c.AppConfig(PathToAppConfig))
                .WithEndpoint<EndpointValid>(b => b.Given((bus, c) =>
                {
                    c.EndpointValid_Name = Configure.EndpointName;

                    var msg = new Message1();
                    bus.SetMessageHeader(msg, "$.diagnostics.licenseexpires", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow.AddMonths(1)));
                    bus.SendLocal(msg);
                }))
                .WithEndpoint<EndpointExpired>(b => b.Given((bus, c) =>
                {
                    c.EndpointExpired_Name = Configure.EndpointName;

                    var msg = new Message1();
                    bus.SetMessageHeader(msg, "$.diagnostics.licenseexpires", DateTimeExtensions.ToWireFormattedString(DateTime.UtcNow.AddMonths(-1)));
                    bus.SendLocal(msg);
                }))
                .WithEndpoint<EndpointNoLicense>(b => b.Given((bus, c) =>
                {
                    c.EndpointNoLicense_Name = Configure.EndpointName;

                    bus.SendLocal(new Message1());
                }))
                .Done(c =>
                {
                    if (TryGetMany("/api/endpoints", out endpoints))
                    {
                        return endpoints.Count == 3;
                    }

                    return false;
                })
                .Run();

            var licenses = new Dictionary<string, string>();

            foreach (var endpoint in endpoints)
            {
                licenses[endpoint.Name] = endpoint.LicenseStatus;
            }

            Assert.AreEqual("valid", licenses[context.EndpointValid_Name]);
            Assert.AreEqual("expired", licenses[context.EndpointExpired_Name]);
            Assert.AreEqual("unknown", licenses[context.EndpointNoLicense_Name]);
        }

        public class EndpointValid : EndpointConfigurationBuilder
        {
            public EndpointValid()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class Message1Handler : IHandleMessages<Message1>
            {
                public void Handle(Message1 message)
                {
                    
                }
            }
        }

        public class EndpointExpired : EndpointConfigurationBuilder
        {
            public EndpointExpired()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class Message1Handler : IHandleMessages<Message1>
            {
                public void Handle(Message1 message)
                {

                }
            }
        }

        public class EndpointNoLicense : EndpointConfigurationBuilder
        {
            public EndpointNoLicense()
            {
                EndpointSetup<DefaultServer>()
                    .AuditTo(Address.Parse("audit"));
            }

            public class Message1Handler : IHandleMessages<Message1>
            {
                public void Handle(Message1 message)
                {

                }
            }
        }

        [Serializable]
        public class Message1 : ICommand
        {
        }
    }
}