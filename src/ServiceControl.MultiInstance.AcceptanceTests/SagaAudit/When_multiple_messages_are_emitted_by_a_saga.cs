namespace ServiceControl.MultiInstance.AcceptanceTests.SagaAudit
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class When_multiple_messages_are_emitted_by_a_saga : AcceptanceTest
    {
        [SetUp]
        public void ConfigSetup()
        {
            // To configure the SagaAudit plugin
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Add("ServiceControl/Queue", ServiceControlInstanceName);
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        [TearDown]
        public void ConfigTeardown()
        {
            // Cleanup the saga audit plugin configuration to not leak into other tests
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Remove("ServiceControl/Queue");
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        [Test]
        public async Task Saga_history_can_be_fetched_on_master()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga {Id = "Id"})))
                .Done(async c =>
                {
                    if (!c.SagaId.HasValue)
                    {
                        return false;
                    }

                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: ServiceControlInstanceName);
                    sagaHistory = result;
                    return result;
                })
                .Run();

            Assert.NotNull(sagaHistory);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(SagaEndpoint.MySaga).FullName, sagaHistory.SagaType);

            var sagaStateChange = sagaHistory.Changes.First();
            Assert.AreEqual("Send", sagaStateChange.InitiatingMessage.Intent);
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditSagaStateChanges(ServiceControlInstanceName);
                    c.AuditProcessedMessagesTo(ServiceControlInstanceName);
                });
            }

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                public MyContext TestContext { get; set; }

                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    TestContext.SagaId = Data.Id;
                    return Task.CompletedTask;
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
                {
                    mapper.ConfigureMapping<MessageInitiatingSaga>(msg => msg.Id).ToSaga(saga => saga.MessageId);
                }
            }

            public class MySagaData : ContainSagaData
            {
                public string MessageId { get; set; }
            }
        }


        public class MessageInitiatingSaga : ICommand
        {
            public string Id { get; set; }
        }


        public class MyContext : ScenarioContext
        {
            public Guid? SagaId { get; set; }
        }
    }
}