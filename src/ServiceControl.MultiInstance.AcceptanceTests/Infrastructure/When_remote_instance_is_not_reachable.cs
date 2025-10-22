namespace ServiceControl.MultiInstance.AcceptanceTests.Infrastructure
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using CompositeViews.Messages;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.Infrastructure.Settings;
    using TestSupport;

    class When_remote_instance_is_not_reachable : AcceptanceTest
    {
        [TestCase(true)]
        [TestCase(false)]
        public async Task Should_not_fail(bool disableHealthChecks)
        {
            var remoteInstanceSetting = new RemoteInstanceSetting("http://localhost:12121");
            CustomServiceControlPrimarySettings = settings =>
            {
                var currentSetting = settings.ServiceControl.RemoteInstanceSettings[0];
                settings.ServiceControl.RemoteInstanceSettings = [currentSetting, remoteInstanceSetting];

                // Toggle the health checks because the behavior should not depend on the health checks running or not running
                settings.ServiceControl.DisableHealthChecks = disableHealthChecks;
                settings.PersisterSpecificSettings.OverrideCustomCheckRepeatTime = TimeSpan.FromSeconds(2);
            };

            PrimaryHostBuilderCustomization = builder =>
            {
                builder.Services.AddKeyedSingleton<Func<HttpMessageHandler>>(remoteInstanceSetting.InstanceId,
                    () => new RemoteNotAvailableHandler());
            };

            //search for the message type
            var searchString = nameof(MyMessage);

            await Define<MyContext>()
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.SendLocal(new MyMessage())))
                .Done(async c => await this.TryGetMany<MessagesView>("/api/messages/search/" + searchString, instanceName: ServiceControlInstanceName))
                .Run();
        }

        class RemoteNotAvailableHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => throw new HttpRequestException(HttpRequestError.ConnectionError);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender() => EndpointSetup<DefaultServerWithAudit>();

            public class MyMessageHandler(MyContext testContext, IReadOnlySettings settings)
                : IHandleMessages<MyMessage>
            {
                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.MessageId = context.MessageId;
                    return Task.CompletedTask;
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}