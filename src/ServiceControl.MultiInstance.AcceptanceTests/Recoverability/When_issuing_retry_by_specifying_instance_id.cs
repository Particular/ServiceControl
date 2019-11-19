namespace ServiceControl.MultiInstance.AcceptanceTests.Recoverability
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using MessageFailures;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests;
    using ServiceBus.Management.AcceptanceTests.EndpointTemplates;
    using ServiceControl.Infrastructure;
    using ServiceControl.Infrastructure.Settings;

    // Tests https://docs.particular.net/servicecontrol/servicecontrol-instances/distributed-instances#advanced-scenarios-multi-region-deployments
    class When_issuing_retry_by_specifying_instance_id : AcceptanceTest
    {
        [Test]
        public async Task Should_be_work()
        {
            // instead of setting up a multiple crazy instances we just use the current instance and rely on it forwarding the instance call to itself
            CustomServiceControlSettings = s => { addressOfItself = s.ApiUrl; };

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

        private string addressOfItself = "TODO";

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => { c.NoRetries(); });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Console.Out.WriteLine("Handling message");
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.LocalAddress = Settings.LocalAddress();
                    Context.MessageId = context.MessageId.Replace(@"\", "-");

                    if (!Context.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        Console.Out.WriteLine("Throwing exception for MyMessage");
                        throw new Exception("Simulated exception");
                    }

                    return Task.FromResult(0);
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