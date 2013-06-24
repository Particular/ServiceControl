namespace ServiceBus.Management.AcceptanceTests.Performance
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Config;
    using ServiceBus.Management.AcceptanceTests.Contexts;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;

    [TestFixture]
    public class Audit_import_performance : AcceptanceTest
    {
        [Test]
        public void Seed_the_input_q()
        {
            var context = new MyContext();

            Scenario.Define(() => context)
                .WithEndpoint<TestEndpoint>(b => b.Given(bus =>
                {
                    Parallel.For(0, 10000, (s, u) => bus.SendLocal(new MyMessage()));

                }))
                .Done(c => c.Complete)
                .Run();

        
        }

        [Test,Explicit("For now")]
        public void Import()
        {
            var context = new MyContext();


            Scenario.Define(() => context)
                .WithEndpoint<ManagementEndpoint>()
                .Done(c =>
                {
                    //var counter = new PerformanceCounter("NServiceBus", "# of msgs successfully processed / sec", "audit", true);

                    //Console.Out.WriteLine("{0} Msg/s",counter.RawValue);
                    return false; //todo
                })
                .Run();

        }



        public class TestEndpoint : EndpointConfigurationBuilder
        {
            public TestEndpoint()
            {
                EndpointSetup<DefaultServer>()
                         .WithConfig<TransportConfig>(c => c.MaximumConcurrencyLevel = 10)
                    .AuditTo(Address.Parse("audit"));
            }


            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                static int numberOfMessagesProcessed;


                public void Handle(MyMessage messageThatIsEnlisted)
                {
                    var current = Interlocked.Increment(ref numberOfMessagesProcessed);
                    if (current == 10000)
                    {
                        Context.Complete = true;
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
            public bool Complete { get; set; }
        }

    }
}