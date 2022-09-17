//namespace ServiceControl.MultiInstance.AcceptanceTests.SagaAudit
//{
//    using System;
//    using System.Configuration;
//    using System.Linq;
//    using System.Threading.Tasks;
//    using AcceptanceTesting;
//    using EventLog;
//    using NServiceBus;
//    using NServiceBus.AcceptanceTesting;
//    using NUnit.Framework;
//    using Raven.Client;
//    using ServiceBus.Management.Infrastructure.Settings;
//    using ServiceControl.SagaAudit;
//    using TestSupport;
//    using TestSupport.EndpointTemplates;

//    [RunOnAllTransports]
//    class When_sending_saga_audit_to_main_instance : AcceptanceTest
//    {
//        string _auditRetentionPeriodOriginalValue = string.Empty;

//        [SetUp]
//        public void ConfigSetup()
//        {
//            // To configure the SagaAudit plugin
//            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
//            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
//            appSettings.Settings.Add("ServiceControl/Queue", ServiceControlInstanceName);

//            //Remove audit retention setting to mimic an upgraded old main instance that has never had audit retention setting,
//            //backing the value up first in order restore it during test teardown.
//            _auditRetentionPeriodOriginalValue = appSettings.Settings["ServiceControl/AuditRetentionPeriod"]?.Value;
//            appSettings.Settings.Remove("ServiceControl/AuditRetentionPeriod");

//            config.Save();
//            ConfigurationManager.RefreshSection("appSettings");
//        }

//        [TearDown]
//        public void ConfigTeardown()
//        {
//            // Cleanup the saga audit plugin configuration to not leak into other tests
//            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
//            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
//            appSettings.Settings.Remove("ServiceControl/Queue");

//            if (_auditRetentionPeriodOriginalValue != null)
//            {
//                appSettings.Settings["ServiceControl/AuditRetentionPeriod"].Value = _auditRetentionPeriodOriginalValue;
//            }

//            config.Save();
//            ConfigurationManager.RefreshSection("appSettings");
//        }

//        [Test]
//        public async Task Saga_history_can_be_fetched_from_main_instance()
//        {
//            SagaHistory sagaHistory = null;

//            var context = await Define<MyContext>()
//                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
//                .Done(async c =>
//                {
//                    if (!c.SagaId.HasValue)
//                    {
//                        return false;
//                    }

//                    var result = await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: ServiceControlInstanceName);
//                    sagaHistory = result;
//                    return result;
//                })
//                .Run();

//            Assert.NotNull(sagaHistory);

//            Assert.AreEqual(context.SagaId, sagaHistory.SagaId);
//            Assert.AreEqual(typeof(SagaEndpoint.MySaga).FullName, sagaHistory.SagaType);

//            var sagaStateChange = sagaHistory.Changes.First();
//            Assert.AreEqual("Send", sagaStateChange.InitiatingMessage.Intent);
//        }

//        [Test]
//        public async Task
//        Check_fails_if_no_audit_retention_is_configured()
//        {
//            //Ensure custom checks are enabled
//            CustomServiceControlSettings = settings =>
//            {
//                settings.DisableHealthChecks = false;
//            };

//            //Override the configuration of the check in the container in order to make it run more frequently for testing purposes.
//            CustomEndpointConfiguration = config =>
//            {
//                config.RegisterComponents(registration =>
//                {
//                    registration.ConfigureComponent((builder) => new AuditRetentionCustomCheck(builder.Build<IDocumentStore>(), builder.Build<Settings>(), TimeSpan.FromSeconds(10)), DependencyLifecycle.SingleInstance);
//                });
//            };

//            SingleResult<EventLogItem> customCheckEventEntry = default;
//            bool sagaAudiDataInMainInstanceIsAvailableForQuery = false;

//            await Define<MyContext>()
//                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
//                .Done(async c =>
//                {
//                    if (!c.SagaId.HasValue)
//                    {
//                        return false;
//                    }

//                    if (sagaAudiDataInMainInstanceIsAvailableForQuery == false)
//                    {
//                        var sagaData =
//                            await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}", instanceName: ServiceControlInstanceName);
//                        sagaAudiDataInMainInstanceIsAvailableForQuery = sagaData.HasResult;
//                        return false;
//                    }

//                    customCheckEventEntry = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/",
//                        e => e.EventType == "CustomCheckFailed" && e.Description.StartsWith("Saga Audit Data Retention"));

//                    return customCheckEventEntry;
//                })
//                .Run();

//            Assert.IsTrue(customCheckEventEntry.Item.RelatedTo.Any(item => item == "/customcheck/Saga Audit Data Retention"), "Event log entry should be related to the Saga Audit Data Retention");
//            Assert.IsTrue(customCheckEventEntry.Item.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")), "Event log entry should be related to the ServiceControl endpoint");
//        }

//        public class SagaEndpoint : EndpointConfigurationBuilder
//        {
//            public SagaEndpoint()
//            {
//                EndpointSetup<DefaultServerWithAudit>(c =>
//                {
//                    c.AuditSagaStateChanges(ServiceControlInstanceName);
//                });
//            }

//            public class MySaga : Saga<MySagaData>, IAmStartedByMessages<MessageInitiatingSaga>
//            {
//                public MyContext TestContext { get; set; }

//                public Task Handle(MessageInitiatingSaga message, IMessageHandlerContext context)
//                {
//                    TestContext.SagaId = Data.Id;
//                    return Task.CompletedTask;
//                }

//                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<MySagaData> mapper)
//                {
//                    mapper.ConfigureMapping<MessageInitiatingSaga>(msg => msg.Id).ToSaga(saga => saga.MessageId);
//                }
//            }

//            public class MySagaData : ContainSagaData
//            {
//                public string MessageId { get; set; }
//            }
//        }


//        public class MessageInitiatingSaga : ICommand
//        {
//            public string Id { get; set; }
//        }


//        public class MyContext : ScenarioContext
//        {
//            public Guid? SagaId { get; set; }
//        }
//    }
//}