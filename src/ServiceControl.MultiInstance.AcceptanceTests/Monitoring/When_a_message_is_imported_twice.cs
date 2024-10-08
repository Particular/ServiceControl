﻿namespace ServiceControl.MultiInstance.AcceptanceTests.Monitoring
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.Pipeline;
    using NUnit.Framework;
    using ServiceControl.Persistence;
    using TestSupport;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_a_message_is_imported_twice : AcceptanceTest
    {
        [Test]
        public async Task Should_register_a_new_endpoint()
        {
            var endpointName = Conventions.EndpointNamingConvention(typeof(Sender));

            EndpointsView endpoint = null;

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EndpointsView>("/api/endpoints", m => m.Name == endpointName);
                    endpoint = result;
                    if (!result)
                    {
                        return false;
                    }

                    return true;
                })
                .Run();

            Assert.That(endpoint?.Name, Is.EqualTo(endpointName));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() =>
                EndpointSetup<DefaultServerWithoutAudit>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(MyMessage), typeof(Receiver));
                });
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver() => EndpointSetup<DefaultServerWithAudit>(c => c.Pipeline.Register(new DuplicateAuditsBehavior(), "Duplicates outgoing audits"));

            class DuplicateAuditsBehavior : Behavior<IAuditContext>
            {
                public override async Task Invoke(IAuditContext context, Func<Task> next)
                {
                    await next();
                    await next();
                }
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context) => Task.CompletedTask;
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
        }
    }
}