namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.EndpointTemplates;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Settings;
    using TestSupport;

    // Tests https://docs.particular.net/servicecontrol/servicecontrol-instances/distributed-instances#advanced-scenarios-multi-region-deployments
    class When_issuing_retry_by_specifying_instance_id : AcceptanceTest
    {
        [Test]
        public async Task Should_be_work()
        {
            string addressOfItself = null;

            // instead of setting up a multiple crazy instances we just use the current instance and rely on it forwarding the instance call to itself
            CustomServiceControlPrimarySettings = s => { addressOfItself = s.ServiceControl.ApiUrl; };

            FailedMessage failure;

            await Define<MyContext>()
                .WithEndpoint<FailureEndpoint>(b => b.When(bus => bus.SendLocal(new MyMessage())).DoNotFailOnErrorMessages())
                .Done(async c =>
                {
                    if (!c.RetryIssued)
                    {
                        var result = await GetFailedMessage(c, ServiceControlInstanceName, FailedMessageStatus.Unresolved);
                        failure = result;
                        if (result)
                        {
                            Assert.That(string.IsNullOrEmpty(addressOfItself), Is.False);

                            c.RetryIssued = true;
                            await this.Post<object>($"/api/errors/{failure.UniqueMessageId}/retry?instance_id={InstanceIdGenerator.FromApiUrl(addressOfItself)}", null, null, ServiceControlInstanceName);
                        }

                        return false;
                    }

                    return await GetFailedMessage(c, ServiceControlInstanceName, FailedMessageStatus.Resolved);
                })
                .Run(TimeSpan.FromMinutes(2));
        }

        Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c, string instance, FailedMessageStatus expectedStatus)
        {
            if (c.MessageId == null)
            {
                return Task.FromResult(SingleResult<FailedMessage>.Empty);
            }

            return this.TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId, f => f.Status == expectedStatus, instance);
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint() => EndpointSetup<DefaultServerWithAudit>(c => { c.NoRetries(); });

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly MyContext testContext;
                readonly IReadOnlySettings settings;
                readonly ReceiveAddresses receiveAddresses;

                public MyMessageHandler(MyContext testContext, IReadOnlySettings settings, ReceiveAddresses receiveAddresses)
                {
                    this.testContext = testContext;
                    this.settings = settings;
                    this.receiveAddresses = receiveAddresses;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.Out.WriteLine("Handling message");
                    testContext.EndpointNameOfReceivingEndpoint = settings.EndpointName();
                    testContext.LocalAddress = receiveAddresses.MainReceiveAddress;
                    testContext.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!testContext.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        Console.Out.WriteLine("Throwing exception for MyMessage");
                        throw new Exception("Simulated exception");
                    }

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

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, EndpointNameOfReceivingEndpoint).ToString();
            public string LocalAddress { get; set; }
            public bool RetryIssued { get; set; }
        }
    }
}