namespace ServiceControl.MultiInstance.AcceptanceTests.SagaAudit
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using EventLog;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.CustomChecks;
    using NUnit.Framework;
    using Raven.Client.Documents;
    using ServiceBus.Management.Infrastructure.Settings;
    using ServiceControl.AcceptanceTests;
    using ServiceControl.AcceptanceTests.TestSupport.EndpointTemplates;
    using ServiceControl.Persistence.RavenDb.SagaAudit;
    using ServiceControl.SagaAudit;


    class AuditRetention : AcceptanceTest
    {
        [Test]
        public async Task
        Check_fails_if_no_audit_retention_is_configured()
        {
            ////Ensure custom checks are enabled
            SetSettings = settings =>
            {
                settings.DisableHealthChecks = false;
                settings.AuditRetentionPeriod = TimeSpan.FromSeconds(5);
            };

            //Override the configuration of the check in the container in order to make it run more frequently for testing purposes.
            CustomizeHostBuilder = hostBuilder => hostBuilder.ConfigureServices((ctx, services) =>
                services.AddTransient<ICustomCheck, AuditRetentionCustomCheck>(provider => new AuditRetentionCustomCheck(provider.GetRequiredService<IDocumentStore>(), provider.GetRequiredService<RavenDBPersisterSettings>(), TimeSpan.FromSeconds(10))));

            SingleResult<EventLogItem> customCheckEventEntry = default;
            bool sagaAudiDataInMainInstanceIsAvailableForQuery = false;

            await Define<MyContext>()
                .WithEndpoint<SagaEndpoint>(b => b.When((bus, c) => bus.SendLocal(new MessageInitiatingSaga { Id = "Id" })))
                .Done(async c =>
                {
                    if (!c.SagaId.HasValue)
                    {
                        return false;
                    }

                    if (sagaAudiDataInMainInstanceIsAvailableForQuery == false)
                    {
                        var sagaData =
                            await this.TryGet<SagaHistory>($"/api/sagas/{c.SagaId}");
                        sagaAudiDataInMainInstanceIsAvailableForQuery = sagaData.HasResult;
                        return false;
                    }

                    customCheckEventEntry = await this.TryGetSingle<EventLogItem>("/api/eventlogitems/",
                        e => e.EventType == "CustomCheckFailed" && e.Description.StartsWith("Saga Audit Data Retention"));

                    return customCheckEventEntry;
                })
                .Run();

            Assert.IsTrue(customCheckEventEntry.Item.RelatedTo.Any(item => item == "/customcheck/Saga Audit Data Retention"), "Event log entry should be related to the Saga Audit Data Retention");
            Assert.IsTrue(customCheckEventEntry.Item.RelatedTo.Any(item => item.StartsWith("/endpoint/Particular.ServiceControl")), "Event log entry should be related to the ServiceControl endpoint");
        }

        public class SagaEndpoint : EndpointConfigurationBuilder
        {
            public SagaEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.AuditSagaStateChanges(Settings.DEFAULT_SERVICE_NAME);
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