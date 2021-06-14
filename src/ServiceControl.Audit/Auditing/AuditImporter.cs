namespace ServiceControl.Audit.Auditing
{
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Transport;

    class AuditImporter : Feature
    {
        public AuditImporter()
        {
            EnableByDefault();
            Prerequisite(c =>
            {
                var settings = c.Settings.Get<Settings>("ServiceControl.Settings");
                return settings.IngestAuditMessages;
            }, "Ingestion of audit messages has been disabled.");
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var settings = context.Settings.Get<Settings>("ServiceControl.Settings");

            var queueBindings = context.Settings.Get<QueueBindings>();
            queueBindings.BindReceiving(settings.AuditQueue);

            context.RegisterStartupTask(b => new StartupTask(b.Build<AuditIngestionComponent>()));
        }

        class StartupTask : FeatureStartupTask
        {
            readonly AuditIngestionComponent auditIngestion;

            public StartupTask(AuditIngestionComponent auditIngestion)
            {
                this.auditIngestion = auditIngestion;
            }

            protected override Task OnStart(IMessageSession session)
            {
                return auditIngestion.Start(session);
            }

            protected override Task OnStop(IMessageSession session)
            {
                return auditIngestion.Stop();
            }
        }
    }
}