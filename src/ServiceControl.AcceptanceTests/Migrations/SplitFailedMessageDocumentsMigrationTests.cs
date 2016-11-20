namespace ServiceBus.Management.AcceptanceTests.Migrations
{
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.Features;
    using NUnit.Framework;
    using ServiceBus.Management.AcceptanceTests.Contexts;

    public class When_a_retry_is_inflight_before_split : AcceptanceTest
    {
        [Test]
        public void It_should_be_added_as_attempt_to_failedmessage_when_it_fails()
        {
            Define<Context>()
                .WithEndpoint<FailureEndpoint>()
                .Done(ctx => true)
                .Run();
        }

        [Test]
        public void It_should_mark_failedmessage_resolved_when_it_succeeds()
        {
            Define<Context>()
                .WithEndpoint<SuccessEndpoint>()
                .Done(ctx => true)
                .Run();
        }

        class Context : ScenarioContext { }

        class TestMessage : ICommand { }

        class FailureEndpoint : EndpointConfigurationBuilder
        {
            public FailureEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                public void Handle(TestMessage message)
                {
                    throw new System.NotImplementedException();
                }
            }
        }

        class SuccessEndpoint : EndpointConfigurationBuilder
        {
            public SuccessEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c => c.DisableFeature<SecondLevelRetries>());
            }

            public class TestMessageHandler : IHandleMessages<TestMessage>
            {
                public void Handle(TestMessage message)
                {
                    throw new System.NotImplementedException();
                }
            }
        }
    }

    public class When_a_retry_for_a_failed_message_succeeds_after_split : AcceptanceTest
    {
        class Context : ScenarioContext { }
    }
}
