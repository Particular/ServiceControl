namespace ServiceControl.MultiInstance.AcceptanceTests.SagaAudit
{
    using System;
    using System.Configuration;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EventLog;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Support;
    using NUnit.Framework;
    using ServiceControl.SagaAudit;
    using TestSupport;
    using TestSupport.EndpointTemplates;

    [RunOnAllTransports]
    class When_sending_saga_audit_to_main_instance : AcceptanceTest
    {
        [SetUp]
        public void ConfigSetup()
        {
            // To configure the SagaAudit plugin
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Add("ServiceControl/Queue", ServiceControlInstanceName);
            MimicSettingsOfAnUpgradedOldMainInstanceThatHasNeverHadAuditRetentionSetting(appSettings);
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        static void MimicSettingsOfAnUpgradedOldMainInstanceThatHasNeverHadAuditRetentionSetting(
            AppSettingsSection appSettings) =>
            appSettings.Settings.Remove("ServiceControl/AuditRetentionPeriod");

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
        public async Task Saga_history_can_be_fetched_from_main_instance()
        {
            SagaHistory sagaHistory = null;

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
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

        [Test]
        public async Task
            Audit_retention_internal_check_fails_when_saga_data_exists_in_main_instance_and_no_audit_retention_is_configured()
        {

            CustomServiceControlSettings = settings =>
            {
                settings.DisableHealthChecks = false;
            };

            var scenarioContext = await WriteSagaAuditDataIntoMainInstance();

            EventLogItem entry = null;

            await scenarioContext
                .Done(async c =>
                {
                    var result = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/",
                        e => e.EventType == "CustomCheckFailed" && e.Description.StartsWith("Saga audit data retention check"));
                    entry = result;
                    return result;
                })
                .Run();

            Assert.IsTrue(entry.RelatedTo.Any(item => item == "/customcheck/Saga audit data retention check"), "Event log entry should be related to the Saga audit data retention check");
            Assert.IsTrue(entry.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")), "Event log entry should be related to the ServiceControl endpoint");
        }

        async Task<IScenarioWithEndpointBehavior<MyContext>> WriteSagaAuditDataIntoMainInstance()
        {
            var scenario = Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })));

            await scenario
                .Done(async c =>
                {
                    if (!c.SagaId.HasValue)
                    {
                        return false;
                    }

                    var sagaAudiDatatInMainInstanceIsAvailableForQuery =
                        await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: ServiceControlInstanceName);
                    return sagaAudiDatatInMainInstanceIsAvailableForQuery;
                })
                .Run();

            return scenario;
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditSagaStateChanges(ServiceControlInstanceName);
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