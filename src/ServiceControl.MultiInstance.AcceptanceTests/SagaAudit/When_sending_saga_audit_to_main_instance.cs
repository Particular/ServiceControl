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
    using ServiceControl.EventLog;
    using ServiceControl.SagaAudit;
    using TestSupport;
    using TestSupport.EndpointTemplates;


    class When_sending_saga_audit_to_main_instance : AcceptanceTest
    {
        string _auditRetentionPeriodOriginalValue = string.Empty;

        [SetUp]
        public void ConfigSetup()
        {
            // To configure the SagaAudit plugin
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            appSettings.Settings.Add("ServiceControl/Queue", ServiceControlInstanceName);

            //Remove audit retention setting to mimic an upgraded old main instance that has never had audit retention setting,
            //backing the value up first in order restore it during test teardown.
            _auditRetentionPeriodOriginalValue = appSettings.Settings["ServiceControl/AuditRetentionPeriod"]?.Value;
            appSettings.Settings.Remove("ServiceControl/AuditRetentionPeriod");

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

            if (_auditRetentionPeriodOriginalValue != null)
            {
                appSettings.Settings["ServiceControl/AuditRetentionPeriod"].Value = _auditRetentionPeriodOriginalValue;
            }

            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        [Test]
        public async Task Saga_history_can_be_fetched_from_main_instance()
        {
            SagaHistory sagaHistory = null;
            EventLogItem eventLog = null;

            CustomServiceControlSettings = settings =>
            {
                settings.DisableHealthChecks = false;
                settings.PersisterSpecificSettings.OverrideCustomCheckRepeatTime = TimeSpan.FromSeconds(2);
            };

            var context = await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
                .Do("GetSagaHistory", async c =>
                {
                    if (!c.SagaId.HasValue)
                    {
                        return false;
                    }

                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: ServiceControlInstanceName);
                    sagaHistory = result;
                    return result;
                })
                .Do("GetEventLog", async c =>
                {
                    var result = await this.TryGetMany<EventLogItem>("/api/eventlogitems/");
                    eventLog = result.Items.FirstOrDefault(e => e.Description.Contains("Saga Audit Destination") && e.Description.Contains("endpoints have reported saga audit data to the ServiceControl Primary instance"));
                    return eventLog != null;
                })
                .Done(c => eventLog != null)
                .Run();

            Assert.NotNull(sagaHistory);
            Assert.NotNull(eventLog);

            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
            Assert.AreEqual(typeof(SagaEndpoint.MySaga).FullName, sagaHistory.SagaType);

            var sagaStateChange = sagaHistory.Changes.First();
            Assert.AreEqual("Send", sagaStateChange.InitiatingMessage.Intent);
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint() =>
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditSagaStateChanges(ServiceControlInstanceName);
                });

            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
            {
                readonly MyContext testContext;

                public MySaga(MyContext testContext) => this.testContext = testContext;

                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
                {
                    testContext.SagaId = Data.Id;
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


        public class MyContext : ScenarioContext, ISequenceContext
        {
            public Guid? SagaId { get; set; }
            public int Step { get; set; }
        }
    }
}