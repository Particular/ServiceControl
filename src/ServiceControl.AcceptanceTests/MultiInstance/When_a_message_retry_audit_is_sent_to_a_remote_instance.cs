namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Config;
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.Infrastructure;
    using ServiceControl.MessageFailures;

    public class When_a_message_retry_audit_is_sent_to_a_remote_instance : AcceptanceTest
    {
        private const string Master = "master";
        private static string AuditMaster = $"{Master}.audit";
        private static string ErrorMaster = $"{Master}.error";
        private const string Remote1 = "remote1";
        private static string AuditRemote = $"{Remote1}.audit1";
        private static string ErrorRemote = $"{Remote1}.error1";

        private string addressOfRemote;


        [Test]
        public async Task Should_mark_as_resolved_on_master()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();

            FailedMessage failure;

            await Define(context, Remote1, Master)
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(async c =>
                {
                    var result = await GetFailedMessage(c);
                    failure = result;
                    if (!result)
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Unresolved)
                    {
                        await IssueRetry(c, () => Post<object>($"/api/errors/{failure.UniqueMessageId}/retry", null, null, Master));
                        return false;
                    }

                    return failure.Status == FailedMessageStatus.Resolved;
                })
                .Run(TimeSpan.FromMinutes(2));
        }

        private void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            switch (instanceName)
            {
                case Remote1:
                    addressOfRemote = settings.ApiUrl;
                    settings.AuditQueue = Address.Parse(AuditRemote);
                    settings.ErrorQueue = Address.Parse(ErrorRemote);
                    break;
                case Master:
                    settings.RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = addressOfRemote,
                            QueueAddress = Remote1
                        }
                    };
                    settings.AuditQueue = Address.Parse(AuditMaster);
                    settings.ErrorQueue = Address.Parse(ErrorMaster);
                    break;
            }
        }

        Task<SingleResult<FailedMessage>> GetFailedMessage(MyContext c)
        {
            if (c.MessageId == null)
            {
                return Task.FromResult(SingleResult<FailedMessage>.Empty);
            }

            return TryGet<FailedMessage>("/api/errors/" + c.UniqueMessageId, null, Master);
        }

        async Task IssueRetry(MyContext c, Func<Task> retryAction)
        {
            if (!c.RetryIssued)
            {
                c.RetryIssued = true;
                await retryAction();
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .WithConfig<TransportConfig>(c => c.MaxRetries = 0)
                    .AuditTo(Address.Parse(AuditRemote))
                    .ErrorTo(Address.Parse(ErrorMaster));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public IBus Bus { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public void Handle(MyMessage message)
                {
                    Console.Out.WriteLine("Handling message");
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.LocalAddress = Settings.LocalAddress().ToString();
                    Context.MessageId = Bus.CurrentMessageContext.Id.Replace(@"\", "-");

                    if (!Context.RetryIssued) //simulate that the exception will be resolved with the retry
                    {
                        Console.Out.WriteLine("Throwing exception for MyMessage");
                        throw new Exception("Simulated exception");
                    }
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }

            public string UniqueMessageId => DeterministicGuid.MakeId(MessageId, Address.Parse(LocalAddress).Queue).ToString();
            public string LocalAddress { get; set; }
            public bool RetryIssued { get; set; }
        }
    }
}