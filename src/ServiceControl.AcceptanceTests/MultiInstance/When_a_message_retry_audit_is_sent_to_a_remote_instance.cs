﻿namespace ServiceBus.Management.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
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
        private const string AuditMaster = "audit";
        private const string ErrorMaster = "error";
        private const string Remote1 = "remote1";
        private const string AuditRemote = "audit1";
        private const string ErrorRemote = "error1";

        private string addressOfRemote;


        [Test]
        public void Should_mark_as_resolved_on_master()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var context = new MyContext();

            FailedMessage failure;

            Define(context, Remote1, Master)
                .WithEndpoint<FailureEndpoint>(b => b.Given(bus => bus.SendLocal(new MyMessage())))
                .Done(c =>
                {
                    if (!GetFailedMessage(c, out failure))
                    {
                        return false;
                    }

                    if (failure.Status == FailedMessageStatus.Unresolved)
                    {
                        IssueRetry(c, () => Post<object>($"/api/recoverability/groups/{failure.FailureGroups.First().Id}/errors/retry", null, null, Master));
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
                    settings.RemoteInstances = new List<Settings.RemoteInstanceSetting>
                    {
                        new Settings.RemoteInstanceSetting
                        {
                            Uri = addressOfRemote,
                            Address = Remote1
                        }
                    };
                    settings.AuditQueue = Address.Parse(AuditMaster);
                    settings.ErrorQueue = Address.Parse(ErrorMaster);
                    break;
            }
        }

        bool GetFailedMessage(MyContext c, out FailedMessage failure)
        {
            failure = null;
            if (c.MessageId == null)
            {
                return false;
            }

            if (!TryGet("/api/errors/" + c.UniqueMessageId, out failure, null, Master))
            {
                return false;
            }
            return true;
        }

        void IssueRetry(MyContext c, Action retryAction)
        {
            if (c.RetryIssued)
            {
                Thread.Sleep(1000); //todo: add support for a "default" delay when Done() returns false
            }
            else
            {
                c.RetryIssued = true;

                retryAction();
            }
        }

        public class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>())
                    .AuditTo(Address.Parse(AuditRemote));
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
            public string PropertyToSearchFor { get; set; }
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